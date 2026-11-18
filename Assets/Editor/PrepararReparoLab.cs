#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

// Prepara a cena do SEBRAELab inteira para o Reparo Lab:
//  - confere XR Origin, XR Device Simulator e chão com Teleportation Area (adiciona o que faltar)
//  - roda "Reparo Lab > Montar cena" (se ainda não foi montada)
//  - encosta Fissura e Mancha em paredes reais e pendura a Lâmpada no teto, usando raios contra a malha do salão
//  - salva a cena e coloca ela em primeiro lugar na Scene List (Build Profile)
// Menu: Reparo Lab > Preparar cena completa
// Linha de comando: Unity.exe -batchmode -quit -projectPath <pasta> -executeMethod PrepararReparoLab.Preparar
public static class PrepararReparoLab
{
    const string CaminhoCena = "Assets/Scenes/Scene1.unity";
    const string PastaXRI = "Assets/Samples/XR Interaction Toolkit/3.4.1/";
    const string PrefabOrigin = PastaXRI + "Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
    const string PrefabSimulador = PastaXRI + "XR Device Simulator/XR Device Simulator.prefab";

    static readonly List<Collider> temporarios = new List<Collider>();
    static Transform lab;
    static readonly StringBuilder relatorio = new StringBuilder();

    [MenuItem("Reparo Lab/Preparar cena completa (passos 2 a 6)")]
    public static void PrepararMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Preparar();
    }

    public static void Preparar()
    {
        relatorio.Clear();
        relatorio.AppendLine("==== REPARO LAB: cena preparada ====");
        Scene cena = EditorSceneManager.OpenScene(CaminhoCena, OpenSceneMode.Single);

        GameObject labObj = GameObject.Find("Sebrae Lab");
        if (labObj == null) { Debug.LogError("Reparo Lab: objeto 'Sebrae Lab' não encontrado em " + CaminhoCena); return; }
        lab = labObj.transform;

        try
        {
            XROrigin xr = GarantirRig();
            GarantirSimulador();
            AdicionarCollidersTemporarios();

            float chaoY = AcharChao(xr, out Renderer rChao);
            GarantirTeleporte(chaoY, rChao);
            OrientarJogador(xr, chaoY);

            if (GameObject.Find("ReparoLab") == null) MontarReparoLab.Montar();
            else relatorio.AppendLine("ReparoLab já existia: não montei de novo, só reposicionei os defeitos.");

            PosicionarDefeitos(xr, chaoY);
        }
        finally
        {
            RemoverCollidersTemporarios(); // nada de Mesh Collider novo na cena salva
        }

        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena);
        AdicionarNaSceneList();
        relatorio.AppendLine("Cena salva: " + CaminhoCena);
        Debug.Log(relatorio.ToString());
    }

    // ---------------- Passo 2: XR Origin, simulador, teleporte ----------------

    static XROrigin GarantirRig()
    {
        XROrigin xr = Object.FindAnyObjectByType<XROrigin>();
        if (xr != null) { relatorio.AppendLine("XR Origin (XR Rig): ok (já existia)."); return xr; }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabOrigin);
        if (prefab == null) throw new System.Exception("Prefab do XR Origin não encontrado em " + PrefabOrigin);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        relatorio.AppendLine("XR Origin (XR Rig): ADICIONADO.");
        return go.GetComponent<XROrigin>();
    }

    static void GarantirSimulador()
    {
        Transform sim = TodosNaCena<Transform>().FirstOrDefault(t => t.name.StartsWith("XR Device Simulator"));
        if (sim != null)
        {
            if (!sim.gameObject.activeSelf) { sim.gameObject.SetActive(true); relatorio.AppendLine("XR Device Simulator: estava desativado, ATIVEI."); }
            else relatorio.AppendLine("XR Device Simulator: ok (já existia).");
            return;
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabSimulador);
        if (prefab == null) { relatorio.AppendLine("AVISO: prefab do XR Device Simulator não encontrado em " + PrefabSimulador); return; }
        PrefabUtility.InstantiatePrefab(prefab);
        relatorio.AppendLine("XR Device Simulator: ADICIONADO.");
    }

    static float AcharChao(XROrigin xr, out Renderer rChao)
    {
        rChao = null;
        Vector3 p = xr.transform.position;
        if (Raio(p + Vector3.up * 1.0f, Vector3.down, 3f, out RaycastHit hit))
        {
            rChao = hit.collider.GetComponent<Renderer>();
            relatorio.AppendLine($"Chão encontrado em y={hit.point.y:F2} ({hit.collider.name}).");
            return hit.point.y;
        }
        relatorio.AppendLine("AVISO: não achei o piso embaixo do XR Origin; usei a altura do próprio XR Origin.");
        return p.y;
    }

    static void GarantirTeleporte(float chaoY, Renderer rChao)
    {
        bool existe = TodosNaCena<TeleportationArea>().Any(t => t.isActiveAndEnabled);
        if (existe) { relatorio.AppendLine("Teleportation Area: ok (já existia)."); return; }

        // Caixa fina cobrindo o piso (BoxCollider, não Mesh Collider), 1,5 cm acima dele
        Bounds b = rChao != null ? rChao.bounds : BoundsDoLab();
        var obj = new GameObject("Chao Teleporte (Reparo Lab)");
        Undo.RegisterCreatedObjectUndo(obj, "Teleporte");
        obj.transform.position = new Vector3(b.center.x, chaoY + 0.005f, b.center.z);
        var box = obj.AddComponent<BoxCollider>();
        box.size = new Vector3(Mathf.Max(b.size.x, 4f), 0.02f, Mathf.Max(b.size.z, 4f));
        var area = obj.AddComponent<TeleportationArea>();
        int camadaTeleporte = InteractionLayerMask.GetMask("Teleport");
        if (camadaTeleporte != 0) area.interactionLayers = camadaTeleporte;
        relatorio.AppendLine($"Teleportation Area: ADICIONADA ('{obj.name}', {box.size.x:F1} x {box.size.z:F1} m).");
    }

    // Se o jogador começa de cara para uma parede, gira o XR Origin para o lado mais aberto do salão
    static void OrientarJogador(XROrigin xr, float chaoY)
    {
        Vector3 o = new Vector3(xr.transform.position.x, chaoY + 1.2f, xr.transform.position.z);
        Vector3 frente = Vector3.ProjectOnPlane(xr.transform.forward, Vector3.up).normalized;
        if (frente == Vector3.zero) frente = Vector3.forward;
        float livreFrente = Livre(o, frente);
        if (livreFrente >= 4.5f) { relatorio.AppendLine($"Jogador já começa olhando para o salão ({livreFrente:F1} m livres à frente)."); return; }

        float melhorAng = 0, melhorLivre = livreFrente;
        for (int a = 0; a < 360; a += 15)
        {
            Vector3 d = Quaternion.AngleAxis(a, Vector3.up) * frente;
            float livre = Mathf.Min(Livre(o, d), Livre(o, Quaternion.AngleAxis(-20, Vector3.up) * d), Livre(o, Quaternion.AngleAxis(20, Vector3.up) * d));
            if (livre > melhorLivre + 1f) { melhorLivre = livre; melhorAng = a; }
        }
        if (melhorAng != 0)
        {
            Undo.RecordObject(xr.transform, "Orientar jogador");
            xr.transform.rotation = Quaternion.AngleAxis(melhorAng, Vector3.up) * xr.transform.rotation;
            relatorio.AppendLine($"Jogador começava de frente para parede ({livreFrente:F1} m); girei o XR Origin {melhorAng}° para o lado aberto ({melhorLivre:F1} m).");
        }
    }

    static float Livre(Vector3 o, Vector3 d) => Raio(o, d, 30f, out RaycastHit h) ? h.distance : 30f;

    // ---------------- Passo 5: posicionar defeitos ----------------

    static void PosicionarDefeitos(XROrigin xr, float chaoY)
    {
        Transform raiz = GameObject.Find("ReparoLab").transform;
        Transform fissura = raiz.Find("Fissura");
        Transform mancha = raiz.Find("Mancha de Infiltracao");
        Transform lampada = raiz.Find("Lampada Queimada");

        Vector3 pe = new Vector3(xr.transform.position.x, chaoY, xr.transform.position.z);
        Vector3 frente = Vector3.ProjectOnPlane(xr.transform.forward, Vector3.up).normalized;
        if (frente == Vector3.zero) frente = Vector3.forward;
        var usados = new List<Vector3>();

        ColocarNaParede(fissura, pe, frente, 1.4f, -1, new Vector2(0.6f, 0.8f), usados);
        ColocarNaParede(mancha, pe, frente, 1.3f, +1, new Vector2(0.9f, 0.7f), usados);
        ColocarLampada(lampada, pe, frente, chaoY);
    }

    // lado: -1 = à esquerda do olhar inicial, +1 = à direita
    static void ColocarNaParede(Transform alvo, Vector3 pe, Vector3 frente, float altura, int lado, Vector2 tam, List<Vector3> usados)
    {
        if (alvo == null) return;
        Vector3 olho = pe + Vector3.up * 1.6f;
        Vector3 origem = pe + Vector3.up * altura;

        // 1ª tentativa: lado pedido, só paredes estruturais. 2ª: qualquer lado à frente. 3ª: 360° e aceita móveis.
        for (int tentativa = 0; tentativa < 3; tentativa++)
        {
            float melhor = float.MinValue;
            RaycastHit melhorHit = default;
            int limite = tentativa < 2 ? 85 : 180;
            for (int a = -limite; a <= limite; a += 5)
            {
                if (tentativa == 0 && a * lado < 10) continue;
                Vector3 d = Quaternion.AngleAxis(a, Vector3.up) * frente;
                if (!Raio(origem, d, 12f, out RaycastHit hit)) continue;
                if (hit.distance < 1.8f || hit.distance > 9f) continue;
                if (Mathf.Abs(hit.normal.y) > 0.2f) continue;            // tem que ser vertical
                float deFrente = Vector3.Dot(hit.normal, -d);
                if (deFrente < 0.35f) continue;                           // nada muito de lado
                bool estrutura = EhEstrutura(hit.collider);
                if (tentativa < 2 && !estrutura) continue;
                if (!Plano(hit, tam)) continue;                           // o retângulo inteiro cabe na parede
                if (!Visivel(olho, hit.point)) continue;                  // dá para ver do ponto de partida
                if (usados.Any(u => Vector3.Distance(u, hit.point) < 2f)) continue;

                float nota = -Mathf.Abs(hit.distance - 4f) - Mathf.Abs(Mathf.Abs(a) - 35f) * 0.04f + deFrente + (estrutura ? 1.5f : 0f);
                if (nota > melhor) { melhor = nota; melhorHit = hit; }
            }
            if (melhor > float.MinValue)
            {
                Undo.RecordObject(alvo, "Posicionar defeito");
                alvo.SetPositionAndRotation(melhorHit.point + melhorHit.normal * 0.03f, Quaternion.LookRotation(-melhorHit.normal, Vector3.up));
                usados.Add(melhorHit.point);
                relatorio.AppendLine($"{alvo.name}: na parede '{melhorHit.collider.name}' em {melhorHit.point:F2}, a {Vector3.Distance(pe, melhorHit.point):F1} m do início (tentativa {tentativa + 1}).");
                return;
            }
        }
        relatorio.AppendLine($"AVISO: não achei parede boa para '{alvo.name}'. Ficou onde o Montar colocou; arraste manualmente.");
    }

    static void ColocarLampada(Transform alvo, Vector3 pe, Vector3 frente, float chaoY)
    {
        if (alvo == null) return;
        Vector3 olho = pe + Vector3.up * 1.6f;
        float melhor = float.MinValue;
        RaycastHit melhorHit = default;

        // Varre pontos à frente do jogador procurando o teto; prefere luminárias/trilhos do modelo
        for (float dist = 1.8f; dist <= 5f; dist += 0.4f)
            for (int a = -50; a <= 50; a += 10)
            {
                Vector3 base_ = pe + Quaternion.AngleAxis(a, Vector3.up) * frente * dist + Vector3.up * 1.6f;
                if (!Raio(base_, Vector3.up, 15f, out RaycastHit hit)) continue;
                if (hit.normal.y > -0.7f) continue;                       // tem que ser superfície virada para baixo
                if (hit.point.y - chaoY < 2.3f) continue;                  // baixo demais (mesa, armário...)
                Vector3 lampPos = new Vector3(hit.point.x, Mathf.Min(hit.point.y - 0.2f, chaoY + 3.2f), hit.point.z);
                if (!Visivel(olho, lampPos)) continue;

                float nota = -Mathf.Abs(dist - 3f) - Mathf.Abs(a) * 0.02f + (EhLuminaria(hit.collider.transform) ? 3f : 0f);
                if (nota > melhor) { melhor = nota; melhorHit = hit; }
            }

        if (melhor == float.MinValue)
        {
            relatorio.AppendLine("AVISO: não achei teto acima da área à frente. A lâmpada ficou onde o Montar colocou; arraste manualmente.");
            return;
        }

        float tetoY = melhorHit.point.y;
        float lampY = Mathf.Min(tetoY - 0.2f, chaoY + 3.2f); // teto muito alto: vira pendente a 3,2 m
        Undo.RecordObject(alvo, "Posicionar lâmpada");
        alvo.position = new Vector3(melhorHit.point.x, lampY, melhorHit.point.z);

        // Pendente: um fio (cilindro sem collider) ligando a lâmpada ao teto
        float fio = tetoY - (lampY + 0.2f);
        if (fio > 0.05f && alvo.Find("Fio") == null)
        {
            var cabo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cabo.name = "Fio";
            Object.DestroyImmediate(cabo.GetComponent<Collider>());
            cabo.transform.SetParent(alvo, false);
            cabo.transform.localPosition = new Vector3(0, 0.2f + fio / 2f, 0);
            cabo.transform.localScale = new Vector3(0.015f, fio / 2f, 0.015f);
            var metal = AssetDatabase.LoadAssetAtPath<Material>("Assets/ReparoLab/Materiais/Metal.mat");
            if (metal != null) cabo.GetComponent<Renderer>().sharedMaterial = metal;
        }

        string onde = EhLuminaria(melhorHit.collider.transform) ? "embaixo da luminária/trilho" : "no teto";
        relatorio.AppendLine($"{alvo.name}: {onde} '{melhorHit.collider.name}', altura {lampY - chaoY:F1} m do piso (teto a {tetoY - chaoY:F1} m).");
    }

    // ---------------- Passo 6: Scene List ----------------

    static void AdicionarNaSceneList()
    {
        var lista = EditorBuildSettings.scenes.Where(s => s.path != CaminhoCena).ToList();
        lista.Insert(0, new EditorBuildSettingsScene(CaminhoCena, true));
        EditorBuildSettings.scenes = lista.ToArray();
        relatorio.AppendLine("Scene List: " + CaminhoCena + " na posição 0.");

        // Unity 6: se o Build Profile ativo tiver lista própria de cenas, atualiza ela também.
        // Feito por reflexão para não quebrar a compilação se a API mudar de versão.
        try
        {
            var tipo = System.AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("UnityEditor.Build.Profile.BuildProfile")).FirstOrDefault(t => t != null);
            var perfil = tipo?.GetMethod("GetActiveBuildProfile")?.Invoke(null, null) as Object;
            if (perfil == null) return;
            var propOverride = tipo.GetProperty("overrideGlobalScenes");
            var propCenas = tipo.GetProperty("scenes");
            if (propOverride == null || propCenas == null || !(bool)propOverride.GetValue(perfil)) return;

            var cenas = ((EditorBuildSettingsScene[])propCenas.GetValue(perfil) ?? new EditorBuildSettingsScene[0])
                .Where(s => s.path != CaminhoCena).ToList();
            cenas.Insert(0, new EditorBuildSettingsScene(CaminhoCena, true));
            propCenas.SetValue(perfil, cenas.ToArray());
            EditorUtility.SetDirty(perfil);
            AssetDatabase.SaveAssets();
            relatorio.AppendLine("Build Profile '" + perfil.name + "' (lista própria de cenas) também atualizado.");
        }
        catch (System.Exception e)
        {
            relatorio.AppendLine("AVISO: não consegui atualizar o Build Profile ativo (" + e.Message + "). Confira em File > Build Profiles > Scene List.");
        }
    }

    // ---------------- Utilitários ----------------

    static IEnumerable<T> TodosNaCena<T>() where T : Component =>
        SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));

    // Colliders temporários só para os raios acharem paredes/teto; removidos antes de salvar
    static void AdicionarCollidersTemporarios()
    {
        temporarios.Clear();
        foreach (var mf in lab.GetComponentsInChildren<MeshFilter>())
            if (mf.sharedMesh != null && mf.GetComponent<Collider>() == null)
                temporarios.Add(mf.gameObject.AddComponent<MeshCollider>());
        Physics.SyncTransforms();
    }

    static void RemoverCollidersTemporarios()
    {
        foreach (var c in temporarios) if (c != null) Object.DestroyImmediate(c);
        temporarios.Clear();
    }

    // Raio que só considera a geometria do Sebrae Lab (ignora XR Rig, mesa, painéis...)
    static bool Raio(Vector3 o, Vector3 d, float max, out RaycastHit hit)
    {
        var hits = Physics.RaycastAll(o, d, max, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
        foreach (var h in hits)
            if (h.collider.transform.IsChildOf(lab)) { hit = h; return true; }
        hit = default;
        return false;
    }

    static bool Visivel(Vector3 olho, Vector3 ponto)
    {
        Vector3 d = ponto - olho;
        if (!Raio(olho, d.normalized, d.magnitude + 0.3f, out RaycastHit h)) return true;
        return h.distance >= d.magnitude - 0.25f;
    }

    // Os 4 cantos do defeito precisam estar na mesma parede plana
    static bool Plano(RaycastHit hit, Vector2 tam)
    {
        Vector3 n = hit.normal;
        Vector3 lado = Vector3.Cross(Vector3.up, n).normalized;
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            {
                Vector3 p = hit.point + lado * (sx * tam.x / 2f) + Vector3.up * (sy * tam.y / 2f) + n * 0.3f;
                if (!Raio(p, -n, 0.6f, out RaycastHit h)) return false;
                if (Mathf.Abs(h.distance - 0.3f) > 0.05f || Vector3.Dot(h.normal, n) < 0.95f) return false;
            }
        return true;
    }

    // Parede de verdade (grande) e não móvel
    static bool EhEstrutura(Collider c)
    {
        var r = c.GetComponent<Renderer>();
        if (r == null) return true;
        Vector3 s = r.bounds.size;
        return Mathf.Max(s.x, s.z) > 3f && s.y > 2.2f;
    }

    static bool EhLuminaria(Transform t)
    {
        for (; t != null && t != lab; t = t.parent)
        {
            string n = t.name.ToLowerInvariant();
            if (n.Contains("lamp") || n.Contains("lâmp") || n.Contains("luz") || n.Contains("lumin") || n.Contains("trilho") || n.Contains("spot") || n.Contains("light"))
                return true;
        }
        return false;
    }

    static Bounds BoundsDoLab()
    {
        var rs = lab.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(lab.position, new Vector3(20, 0, 20));
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }
}
#endif
