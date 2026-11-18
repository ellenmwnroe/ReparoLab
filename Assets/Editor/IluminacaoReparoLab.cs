#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.XR.CoreUtils;

// Iluminação leve para Meta Quest 2 (URP), sem mudar a lógica do jogo.
// Menu Reparo Lab > Iluminação:
//   1. Aplicar iluminação   -> ambiente em gradiente quente, direcional quente, estáticos, probes, emissivos, spot da mesa
//   2. Fazer bake           -> Progressive GPU (CPU se não houver), resolução baixa; cancela e volta para "sem bake" se passar de 10 min
//   Desfazer bake           -> apaga os lightmaps e fica a versão sem bake
public static class IluminacaoReparoLab
{
    const string Pasta = "Assets/ReparoLab/Iluminacao";
    const string CaminhoFbx = "Assets/Sebraelab3D/Sebrae Lab.fbx";
    const double LimiteBake = 600; // 10 minutos

    static readonly Color Quente = new Color(1f, 0.886f, 0.722f); // #FFE2B8

    // ======================================================================
    [MenuItem("Reparo Lab/Iluminação/1. Aplicar iluminação (sem bake)")]
    public static void Aplicar()
    {
        var log = new StringBuilder("==== REPARO LAB: iluminação aplicada ====\n");
        Transform lab = GameObject.Find("Sebrae Lab")?.transform;
        Transform raiz = GameObject.Find("ReparoLab")?.transform;
        XROrigin xr = Object.FindAnyObjectByType<XROrigin>();
        if (lab == null || raiz == null || xr == null) { EditorUtility.DisplayDialog("Reparo Lab", "Não achei Sebrae Lab / ReparoLab / XR Origin na cena.", "OK"); return; }
        if (!AssetDatabase.IsValidFolder(Pasta)) AssetDatabase.CreateFolder("Assets/ReparoLab", "Iluminacao");

        Vector3 inicio = xr.transform.position;
        Transform mesa = raiz.Find("Mesa de Ferramentas");

        GarantirUVsDeLightmap(lab, log);
        ConfigurarURP(log);
        ConfigurarAmbiente(log);
        ConfigurarDirecional(lab, inicio, log);
        MarcarEstaticos(lab, mesa, log);
        EmissivoNasLuminarias(lab, log);
        CriarLuzesBaked(raiz, mesa, inicio, log);
        CriarProbes(raiz, mesa, inicio, log);
        ConfigurarLightingSettings(log);

        Salvar();
        log.Append(ListaDeLuzes());
        Debug.Log(log.ToString());
    }

    // ======================================================================
    [MenuItem("Reparo Lab/Iluminação/2. Fazer bake (máx. 10 min)")]
    public static void Bake()
    {
        if (Lightmapping.isRunning) { Debug.LogWarning("Reparo Lab: já tem um bake rodando."); return; }
        if (!Lightmapping.TryGetLightingSettings(out LightingSettings atual) || atual == null || !atual.bakedGI)
        {
            EditorUtility.DisplayDialog("Reparo Lab", "Rode antes 'Reparo Lab > Iluminação > 1. Aplicar iluminação'.", "OK");
            return;
        }
        Salvar();
        inicioBake = EditorApplication.timeSinceStartup;
        terminou = false;
        Lightmapping.bakeCompleted -= AoTerminar;
        Lightmapping.bakeCompleted += AoTerminar;
        EditorApplication.update -= Vigiar;
        EditorApplication.update += Vigiar;
        if (!Lightmapping.BakeAsync())
        {
            Lightmapping.bakeCompleted -= AoTerminar;
            EditorApplication.update -= Vigiar;
            VersaoSemBake("o Unity não conseguiu iniciar o bake");
            return;
        }
        string motor = Lightmapping.TryGetLightingSettings(out LightingSettings cfg) && cfg != null ? cfg.lightmapper.ToString() : "?";
        Debug.Log("Reparo Lab: bake iniciado (" + motor + "). Acompanhe a barra no canto inferior direito. Limite: 10 min.");
    }

    [MenuItem("Reparo Lab/Iluminação/Desfazer bake (versão sem bake)")]
    public static void Desfazer() => VersaoSemBake("pedido manual");

    static double inicioBake;
    static bool terminou;

    static void Vigiar()
    {
        if (terminou) { EditorApplication.update -= Vigiar; return; }
        double t = EditorApplication.timeSinceStartup - inicioBake;
        if (Lightmapping.isRunning && t > LimiteBake)
        {
            terminou = true;
            EditorApplication.update -= Vigiar;
            Lightmapping.bakeCompleted -= AoTerminar;
            Lightmapping.Cancel();
            VersaoSemBake("o bake passou de 10 minutos");
        }
        else if (!Lightmapping.isRunning && t > 5 && !terminou)
        {
            // terminou sem disparar o evento (cancelado ou falhou)
            EditorApplication.delayCall += () => { if (!terminou) { terminou = true; Verificar(t); } };
            EditorApplication.update -= Vigiar;
        }
    }

    static void AoTerminar()
    {
        if (terminou) return;
        terminou = true;
        Lightmapping.bakeCompleted -= AoTerminar;
        Verificar(EditorApplication.timeSinceStartup - inicioBake);
    }

    static void Verificar(double segundos)
    {
        if (LightmapSettings.lightmaps == null || LightmapSettings.lightmaps.Length == 0)
        {
            VersaoSemBake("o bake terminou sem gerar lightmaps");
            return;
        }
        Salvar();
        var sb = new StringBuilder();
        sb.AppendLine($"==== REPARO LAB: BAKE CONCLUÍDO em {segundos / 60.0:F1} min — {LightmapSettings.lightmaps.Length} lightmap(s) ====");
        var tamanhos = LightmapSettings.lightmaps.Where(l => l.lightmapColor != null).Select(l => l.lightmapColor.width + "x" + l.lightmapColor.height);
        sb.AppendLine("Lightmaps: " + string.Join(", ", tamanhos));
        sb.AppendLine("Baked: luz indireta + sombras da direcional nas superfícies estáticas, emissivos das luminárias, spot da mesa, luz de preenchimento, Light Probes e Reflection Probe.");
        sb.Append(ListaDeLuzes());
        Debug.Log(sb.ToString());
    }

    static void VersaoSemBake(string motivo)
    {
        Lightmapping.Clear();
        foreach (var l in LuzesDaCena())
        {
            if (l.type == LightType.Directional)
            {
                l.lightmapBakeType = LightmapBakeType.Realtime;
                EditorUtility.SetDirty(l);
            }
            else if (l.name == NomeSpotMesa || l.name == NomePreenchimento || l.name == NomeDestaqueFissura)
            {
                l.gameObject.SetActive(false); // luzes baked não fazem nada sem bake
            }
        }
        if (Lightmapping.TryGetLightingSettings(out LightingSettings ls) && ls != null) { ls.bakedGI = false; EditorUtility.SetDirty(ls); }
        Salvar();
        Debug.LogWarning("==== REPARO LAB: VERSÃO SEM BAKE (" + motivo + ") — ambiente em gradiente + direcional em tempo real + emissivos ====\n" + ListaDeLuzes());
    }

    // ======================================================================
    // Etapas
    // ======================================================================

    static void GarantirUVsDeLightmap(Transform lab, StringBuilder log)
    {
        bool faltaUV = lab.GetComponentsInChildren<MeshFilter>(true)
            .Any(mf => mf.sharedMesh != null && (mf.sharedMesh.uv2 == null || mf.sharedMesh.uv2.Length == 0));
        var imp = AssetImporter.GetAtPath(CaminhoFbx) as ModelImporter;
        if (faltaUV && imp != null && !imp.generateSecondaryUV)
        {
            imp.generateSecondaryUV = true;
            imp.SaveAndReimport();
            log.AppendLine("Modelo sem UV de lightmap: ativei Generate Lightmap UVs em " + CaminhoFbx + ".");
        }
        else log.AppendLine("UVs de lightmap: ok.");
    }

    static void ConfigurarURP(StringBuilder log)
    {
        var assets = new HashSet<RenderPipelineAsset> { GraphicsSettings.defaultRenderPipeline, QualitySettings.renderPipeline };
        foreach (var a in assets)
        {
            if (a == null) continue;
            var so = new SerializedObject(a);
            Definir(so, "m_AdditionalLightsRenderingMode", 1);   // Per Pixel (estava Disabled: a luz da lâmpada consertada não iluminava)
            Definir(so, "m_AdditionalLightsPerObjectLimit", 1);
            Definir(so, "m_AdditionalLightShadowsSupported", false);
            Definir(so, "m_MainLightShadowmapResolution", 1024); // estava 4096
            Definir(so, "m_SoftShadowsSupported", false);
            Definir(so, "m_MixedLightingSupported", true);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(a);
            log.AppendLine("URP '" + a.name + "': luzes adicionais Per Pixel (1 por objeto), sombra principal 1024 sem suavização.");
        }
        AssetDatabase.SaveAssets();
    }

    static void Definir(SerializedObject so, string campo, int valor) { var p = so.FindProperty(campo); if (p != null) p.intValue = valor; }
    static void Definir(SerializedObject so, string campo, bool valor) { var p = so.FindProperty(campo); if (p != null) p.boolValue = valor; }

    static void ConfigurarAmbiente(StringBuilder log)
    {
        string caminho = Pasta + "/Ceu Quente.mat";
        var ceu = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        Shader procedural = Shader.Find("Skybox/Procedural");
        if (ceu == null && procedural != null)
        {
            ceu = new Material(procedural);
            ceu.SetFloat("_SunDisk", 0f);
            ceu.SetFloat("_AtmosphereThickness", 1.3f);
            ceu.SetColor("_SkyTint", new Color(0.78f, 0.62f, 0.5f));
            ceu.SetColor("_GroundColor", new Color(0.45f, 0.38f, 0.33f));
            ceu.SetFloat("_Exposure", 1.1f);
            AssetDatabase.CreateAsset(ceu, caminho);
        }
        if (ceu != null) RenderSettings.skybox = ceu;

        RenderSettings.ambientMode = AmbientMode.Trilight; // Gradient
        RenderSettings.ambientSkyColor = new Color(0.96f, 0.86f, 0.72f);
        RenderSettings.ambientEquatorColor = new Color(0.66f, 0.56f, 0.47f);
        RenderSettings.ambientGroundColor = new Color(0.32f, 0.27f, 0.22f);
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.defaultReflectionResolution = 64;
        RenderSettings.fog = false;
        log.AppendLine("Ambiente: Gradient quente (céu/horizonte/chão) + skybox procedural quente sem sol.");
    }

    static void ConfigurarDirecional(Transform lab, Vector3 inicio, StringBuilder log)
    {
        var dirs = LuzesDaCena().Where(l => l.type == LightType.Directional).ToList();
        Light sol = dirs.FirstOrDefault();
        if (sol == null)
        {
            sol = new GameObject("Directional Light").AddComponent<Light>();
            sol.type = LightType.Directional;
            Undo.RegisterCreatedObjectUndo(sol.gameObject, "Sol");
        }
        foreach (var extra in dirs.Skip(1)) { extra.gameObject.SetActive(false); log.AppendLine("Direcional extra desativada: " + extra.name); }

        Undo.RecordObject(sol, "Sol");
        Undo.RecordObject(sol.transform, "Sol");
        sol.color = Quente;
        sol.intensity = 1.1f;
        sol.lightmapBakeType = LightmapBakeType.Mixed;
        sol.shadows = LightShadows.Hard;
        sol.shadowStrength = 0.8f;
        RenderSettings.sun = sol;

        // Entra pela entrada do salão: vai da entrada em direção ao ponto de partida, inclinada 40° para baixo
        Transform entrada = lab.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => t.name.Trim() == "Porta Entrada") ??
            lab.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.Trim() == "Entrada");
        Renderer rEntrada = entrada != null ? entrada.GetComponentInChildren<Renderer>() : null;
        if (rEntrada != null)
        {
            Vector3 d = inicio - rEntrada.bounds.center;
            d.y = 0;
            if (d.sqrMagnitude > 0.01f)
            {
                sol.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up) * Quaternion.Euler(40f, 0f, 0f);
                log.AppendLine("Direcional: #FFE2B8, intensidade 1,1, Mixed, sombra Hard; entra pela '" + entrada.name.Trim() + "' (40° de inclinação).");
                EditorUtility.SetDirty(sol);
                return;
            }
        }
        log.AppendLine("Direcional: #FFE2B8, intensidade 1,1, Mixed, sombra Hard (ângulo mantido: entrada não encontrada).");
        EditorUtility.SetDirty(sol);
    }

    static void MarcarEstaticos(Transform lab, Transform mesa, StringBuilder log)
    {
        const StaticEditorFlags flags = StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic |
                                        StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic |
                                        StaticEditorFlags.ReflectionProbeStatic;
        int n = 0;
        foreach (var r in lab.GetComponentsInChildren<MeshRenderer>(true))
        {
            GameObjectUtility.SetStaticEditorFlags(r.gameObject, flags);
            r.receiveGI = ReceiveGI.Lightmaps;
            // Piso externo é enorme e quase não aparece: menos texels no lightmap
            Vector3 s = r.bounds.size;
            r.scaleInLightmap = (s.x * s.z > 600f && r.name.Contains("Fora")) ? 0.1f : 1f;
            n++;
        }
        if (mesa != null)
        {
            GameObjectUtility.SetStaticEditorFlags(mesa.gameObject, flags);
            n++;
        }
        log.AppendLine("Estáticos (Contribute GI + Batching + Occlusion + Reflection Probe): " + n + " objetos do SEBRAELab + mesa de ferramentas.");
    }

    static void EmissivoNasLuminarias(Transform lab, StringBuilder log)
    {
        string caminho = Pasta + "/Luminaria Emissiva.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            Color base_ = new Color(0.95f, 0.92f, 0.86f);
            mat.SetColor("_BaseColor", base_);
            mat.color = base_;
            AssetDatabase.CreateAsset(mat, caminho);
        }
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(1f, 0.93f, 0.8f) * 2.2f); // ilumina o bake; na tela fica branco quente (HDR desligado: sem bloom)
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        EditorUtility.SetDirty(mat);

        var nomes = new List<string>();
        foreach (var r in lab.GetComponentsInChildren<MeshRenderer>(true))
        {
            string n = r.name.ToLowerInvariant();
            bool luminaria = n.Contains("lampada") || n.Contains("lâmpada") || n.Contains("luminaria") || n.Contains("trilho");
            for (Transform t = r.transform.parent; !luminaria && t != null && t != lab; t = t.parent)
            {
                string p = t.name.ToLowerInvariant();
                luminaria = p.Contains("lampada") || p.Contains("luminaria") || p.Contains("trilho");
            }
            if (!luminaria) continue;
            Undo.RecordObject(r, "Emissivo");
            r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
            nomes.Add(r.name);
        }
        log.AppendLine(nomes.Count > 0
            ? "Luminárias emissivas (sem luz real, entram no bake): " + string.Join(", ", nomes.Distinct())
            : "AVISO: não achei luminárias pelo nome no modelo; nenhum emissivo aplicado.");
    }

    const string NomeSpotMesa = "Spot Mesa (baked)";
    const string NomePreenchimento = "Luz Preenchimento (baked)";
    const string NomeDestaqueFissura = "Destaque Fissura (baked)";

    static void CriarLuzesBaked(Transform raiz, Transform mesa, Vector3 inicio, StringBuilder log)
    {
        Transform pai = Pai(raiz);

        if (mesa != null)
        {
            var spot = ObterLuz(pai, NomeSpotMesa);
            spot.type = LightType.Spot;
            spot.lightmapBakeType = LightmapBakeType.Baked;
            spot.color = new Color(1f, 0.9f, 0.75f);
            spot.intensity = 2.2f;
            spot.range = 4.5f;
            spot.spotAngle = 75f;
            spot.innerSpotAngle = 35f;
            spot.shadows = LightShadows.Soft; // sombra baked: custo zero no Quest
            spot.transform.position = mesa.position + Vector3.up * 2.4f;
            spot.transform.rotation = Quaternion.LookRotation(Vector3.down, mesa.forward);
            log.AppendLine("Spot suave baked sobre a mesa de ferramentas.");
        }

        var fill = ObterLuz(pai, NomePreenchimento);
        fill.type = LightType.Point;
        fill.lightmapBakeType = LightmapBakeType.Baked;
        fill.color = Quente;
        fill.intensity = 1.1f;
        fill.range = 10f;
        fill.shadows = LightShadows.Soft;
        Vector3 frente = mesa != null ? Vector3.ProjectOnPlane(mesa.position - inicio, Vector3.up).normalized : Vector3.forward;
        fill.transform.position = inicio + frente * 2.5f + Vector3.up * 2.3f;
        log.AppendLine("Luz pontual de preenchimento baked no salão (sem custo em tempo real).");

        // Destaque baked na parede da fissura: clareia a parede em volta para a rachadura preta ter contraste
        Transform fissura = raiz.Find("Fissura");
        if (fissura != null)
        {
            var destaque = ObterLuz(pai, NomeDestaqueFissura);
            destaque.type = LightType.Spot;
            destaque.lightmapBakeType = LightmapBakeType.Baked;
            destaque.color = new Color(1f, 0.95f, 0.88f);
            destaque.intensity = 1.6f;
            destaque.range = 4f;
            destaque.spotAngle = 70f;
            destaque.innerSpotAngle = 30f;
            destaque.shadows = LightShadows.Soft;
            // 1,6 m à frente da parede e 0,6 m acima, apontando para a rachadura (forward da fissura entra na parede)
            Vector3 pos = fissura.position - fissura.forward * 1.6f + Vector3.up * 0.6f;
            destaque.transform.position = pos;
            destaque.transform.rotation = Quaternion.LookRotation(fissura.position - pos, Vector3.up);
            log.AppendLine("Spot de destaque baked na parede da fissura.");
        }
    }

    static void CriarProbes(Transform raiz, Transform mesa, Vector3 inicio, StringBuilder log)
    {
        Transform pai = Pai(raiz);
        float chao = inicio.y;

        // ---- Light Probes: mesa, ponto de partida e perto de cada defeito ----
        var pontos = new List<Vector3>();
        void Grade(Vector3 centro, float passo, int metade, float[] alturas)
        {
            for (int x = -metade; x <= metade; x++)
                for (int z = -metade; z <= metade; z++)
                    foreach (float h in alturas)
                        pontos.Add(new Vector3(centro.x + x * passo, chao + h, centro.z + z * passo));
        }
        if (mesa != null) Grade(mesa.position, 1.2f, 1, new[] { 0.4f, 1.3f, 2.3f });
        Grade(inicio, 1.6f, 1, new[] { 0.4f, 1.6f });
        foreach (var d in raiz.GetComponentsInChildren<Defeito>(true))
        {
            Vector3 frente = d.transform.forward; // aponta para dentro da parede
            Vector3 lado = Vector3.Cross(Vector3.up, frente).normalized;
            Vector3 c = d.transform.position - frente * 0.5f;
            foreach (float l in new[] { -0.7f, 0.7f })
                foreach (float h in new[] { -0.6f, 0.6f })
                    pontos.Add(c + lado * l + Vector3.up * h);
        }

        Transform grupo = pai.Find("Light Probes");
        if (grupo == null)
        {
            grupo = new GameObject("Light Probes").transform;
            Undo.RegisterCreatedObjectUndo(grupo.gameObject, "Probes");
            grupo.SetParent(pai, false);
        }
        grupo.position = Vector3.zero;
        grupo.rotation = Quaternion.identity;
        grupo.localScale = Vector3.one;
        if (!grupo.TryGetComponent(out LightProbeGroup lpg)) lpg = grupo.gameObject.AddComponent<LightProbeGroup>();
        lpg.probePositions = pontos.Select(p => grupo.InverseTransformPoint(p)).ToArray();
        log.AppendLine("Light Probes: " + pontos.Count + " pontos (mesa, partida e defeitos) para as ferramentas e objetos dinâmicos.");

        // ---- 1 Reflection Probe baked ----
        Transform rp = pai.Find("Reflection Probe");
        if (rp == null)
        {
            rp = new GameObject("Reflection Probe").transform;
            Undo.RegisterCreatedObjectUndo(rp.gameObject, "Reflexo");
            rp.SetParent(pai, false);
        }
        Vector3 centro = mesa != null ? mesa.position : inicio;
        rp.position = new Vector3(centro.x, chao + 1.5f, centro.z);
        if (!rp.TryGetComponent(out ReflectionProbe probe)) probe = rp.gameObject.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Baked;
        probe.resolution = 128;
        probe.size = new Vector3(16f, 3.5f, 16f);
        probe.boxProjection = false;
        probe.hdr = false;
        probe.importance = 1;
        log.AppendLine("Reflection Probe baked (128 px) no centro da área de jogo.");
    }

    static void ConfigurarLightingSettings(StringBuilder log)
    {
        string caminho = Pasta + "/ReparoLab Lighting.lighting";
        var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(caminho);
        if (ls == null)
        {
            ls = new LightingSettings { name = "ReparoLab Lighting" };
            AssetDatabase.CreateAsset(ls, caminho);
        }
        bool temGpu = SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null && SystemInfo.graphicsMemorySize >= 2000;
        ls.lightmapper = temGpu ? LightingSettings.Lightmapper.ProgressiveGPU : LightingSettings.Lightmapper.ProgressiveCPU;
        ls.bakedGI = true;
        ls.realtimeGI = false;
        ls.mixedBakeMode = MixedLightingMode.Subtractive; // o mais barato no Quest
        ls.lightmapResolution = 12f;    // texels por unidade (baixo = bake rápido)
        ls.lightmapPadding = 2;
        ls.lightmapMaxSize = 1024;
        ls.lightmapCompression = LightmapCompression.NormalQuality;
        ls.directionalityMode = LightmapsMode.NonDirectional;
        ls.directSampleCount = 16;
        ls.indirectSampleCount = 128;
        ls.environmentSampleCount = 64;
        ls.maxBounces = 2;
        ls.indirectScale = 1.25f; // interior um pouco mais claro
        ls.ao = false;
        ls.filteringMode = LightingSettings.FilterMode.Auto;
        ls.autoGenerate = false;
        EditorUtility.SetDirty(ls);
        Lightmapping.lightingSettings = ls;
        log.AppendLine($"Lighting Settings: {ls.lightmapper}, 12 texels/unidade, lightmap máx. 1024, 16 amostras diretas / 128 indiretas, 2 rebatidas (indireta x1,25), Subtractive, não-direcional.");
    }

    // ======================================================================
    // Utilitários
    // ======================================================================

    static Transform Pai(Transform raiz)
    {
        Transform pai = raiz.Find("Iluminacao");
        if (pai == null)
        {
            pai = new GameObject("Iluminacao").transform;
            Undo.RegisterCreatedObjectUndo(pai.gameObject, "Iluminação");
            pai.SetParent(raiz, false);
        }
        return pai;
    }

    static Light ObterLuz(Transform pai, string nome)
    {
        Transform t = pai.Find(nome);
        if (t == null)
        {
            t = new GameObject(nome).transform;
            Undo.RegisterCreatedObjectUndo(t.gameObject, nome);
            t.SetParent(pai, false);
        }
        t.gameObject.SetActive(true);
        if (!t.TryGetComponent(out Light l)) l = t.gameObject.AddComponent<Light>();
        Undo.RecordObject(l, nome);
        return l;
    }

    static IEnumerable<Light> LuzesDaCena() =>
        SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light>(true));

    static string ListaDeLuzes()
    {
        var sb = new StringBuilder("Luzes na cena:\n");
        foreach (var l in LuzesDaCena())
            sb.AppendLine($"  - {l.name}: {l.type}, modo {l.lightmapBakeType}, sombra {l.shadows}, intensidade {l.intensity:F1}{(l.gameObject.activeInHierarchy ? "" : " (desativada / só acende no jogo)")}");
        return sb.ToString();
    }

    static void Salvar()
    {
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
    }
}
#endif
