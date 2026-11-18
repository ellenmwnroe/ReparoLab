#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Menu: Reparo Lab > Montar tutorial da Lia
// Cria as fontes TMP (Fredoka/Nunito), o painel de diálogo, a seta guia, os brilhos das ferramentas
// e o objeto "Tutorial Lia" já ligado aos defeitos. Pode rodar de novo: ele substitui o tutorial antigo.
public static class MontarTutorialLia
{
    const string PastaFontes = "Assets/ReparoLab/Fontes";
    const string PastaMateriais = "Assets/ReparoLab/Materiais";
    const string PastaMalhas = "Assets/ReparoLab/Malhas";

    static readonly Color Laranja = new Color(1f, 0.55f, 0.12f);
    static readonly Color FundoEscuro = new Color(0.08f, 0.09f, 0.15f, 0.96f);

    [MenuItem("Reparo Lab/Criar fontes TMP (Fredoka + Nunito)")]
    public static void CriarFontesMenu()
    {
        CriarFonte("Fredoka-SemiBold");
        CriarFonte("Nunito-Regular");
        Debug.Log("Reparo Lab: fontes TMP criadas em " + PastaFontes);
    }

    [MenuItem("Reparo Lab/Montar tutorial da Lia")]
    public static void Montar()
    {
        Transform raiz = GameObject.Find("ReparoLab")?.transform;
        if (raiz == null) { EditorUtility.DisplayDialog("Reparo Lab", "Rode antes 'Reparo Lab > Preparar cena completa'.", "OK"); return; }

        TMP_FontAsset titulo = CriarFonte("Fredoka-SemiBold");
        TMP_FontAsset corpo = CriarFonte("Nunito-Regular");
        if (titulo != null && corpo != null)
        {
            if (titulo.fallbackFontAssetTable == null) titulo.fallbackFontAssetTable = new List<TMP_FontAsset>();
            if (!titulo.fallbackFontAssetTable.Contains(corpo))
            {
                titulo.fallbackFontAssetTable.Add(corpo);
                EditorUtility.SetDirty(titulo);
            }
        }

        // Remove montagem anterior
        var antigo = raiz.Find("Tutorial Lia");
        if (antigo != null) Undo.DestroyObjectImmediate(antigo.gameObject);

        XROrigin xr = Object.FindAnyObjectByType<XROrigin>();
        Transform cabeca = xr != null && xr.Camera != null ? xr.Camera.transform : null;

        var tutorialObj = new GameObject("Tutorial Lia");
        Undo.RegisterCreatedObjectUndo(tutorialObj, "Tutorial Lia");
        tutorialObj.transform.SetParent(raiz, false);

        DialogoVN dialogo = CriarPainel(tutorialObj.transform, titulo, corpo, cabeca, xr);
        CriarSeta(tutorialObj.transform, titulo, out Transform seta, out Transform cone, out TMP_Text textoSeta);

        Material brilho = MaterialBrilho();
        var guia = tutorialObj.AddComponent<GuiaTutorial>();
        guia.dialogo = dialogo;
        guia.gerenciador = raiz.GetComponentInChildren<GerenciadorJogo>(true);
        guia.xrOrigin = xr;
        guia.cabeca = cabeca;
        guia.botaoLiberar = raiz.Find("Botao Liberar Espaco");
        guia.seta = seta;
        guia.cone = cone;
        guia.textoSeta = textoSeta;

        Transform lampada = raiz.Find("Lampada Queimada");
        guia.missoes = new[]
        {
            Missao(raiz, "Fissura", "Visual (rachadura)", null, "Espatula", "a rachadura", "a espátula", brilho),
            Missao(raiz, "Mancha de Infiltracao", "Visual (mancha)", null, "Rolo de Tinta", "a mancha", "o rolo de tinta", brilho),
            Missao(raiz, "Lampada Queimada", "Lampada Apagada", lampada != null ? lampada.Find("Soquete") : null, "Lampada Nova", "o soquete da lâmpada", "a lâmpada nova", brilho),
        };

        // Checklist e avisos passam para o painel da Lia; painéis antigos ficam escondidos
        if (guia.gerenciador != null)
        {
            Undo.RecordObject(guia.gerenciador, "Gerenciador");
            guia.gerenciador.textoChecklist = textoStatus;
            guia.gerenciador.textoAviso = textoAviso;
            guia.gerenciador.textoInfo = null;
            guia.gerenciador.checklistCompacto = true;
            EditorUtility.SetDirty(guia.gerenciador);
        }
        foreach (string nomePainel in new[] { "Painel Info", "Painel Checklist" })
        {
            Transform p = raiz.Find(nomePainel);
            if (p != null && p.gameObject.activeSelf) { Undo.RecordObject(p.gameObject, "Esconder"); p.gameObject.SetActive(false); }
        }

        // Menu de teste do projeto base ("Bem-vindo", UI Toolkit): escondido, não faz parte do Reparo Lab
        var testes = new List<TestUI>();
        foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects()) testes.AddRange(r.GetComponentsInChildren<TestUI>());
        foreach (var teste in testes)
        {
            Undo.RecordObject(teste.gameObject, "Esconder menu de teste");
            teste.gameObject.SetActive(false);
            Debug.Log("Reparo Lab: menu de teste do projeto base ('" + teste.name + "') escondido.");
        }

        // Lâmpada nova também não cai no limbo (e conta como "ferramenta errada" se encostar em outro defeito)
        Transform lampadaNova = raiz.Find("Lampada Nova");
        if (lampadaNova != null && lampadaNova.GetComponent<Ferramenta>() == null)
            Undo.AddComponent<Ferramenta>(lampadaNova.gameObject).tipo = "Lampada";

        // Fonte Nunito nos painéis antigos, para ficar tudo com a mesma cara
        if (corpo != null)
            foreach (var t in raiz.GetComponentsInChildren<TMP_Text>(true))
                if (!t.transform.IsChildOf(tutorialObj.transform)) { Undo.RecordObject(t, "Fonte"); t.font = corpo; }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = tutorialObj;
        Debug.Log("==== REPARO LAB: tutorial da Lia montado e cena salva ====");
    }

    // ================= Missões =================

    static GuiaTutorial.Missao Missao(Transform raiz, string defeito, string visual, Transform ponto, string ferramenta, string nomeDefeito, string nomeFerramenta, Material brilho)
    {
        var m = new GuiaTutorial.Missao { nomeDefeito = nomeDefeito, nomeFerramenta = nomeFerramenta };
        Transform d = raiz.Find(defeito);
        if (d == null) { Debug.LogWarning("Reparo Lab: não achei '" + defeito + "'"); return m; }
        m.defeito = d.GetComponent<Defeito>();
        m.visualDefeito = d.Find(visual);
        m.pontoReparo = ponto != null ? ponto : d;

        Transform f = raiz.Find(ferramenta);
        if (f == null) { Debug.LogWarning("Reparo Lab: não achei a ferramenta '" + ferramenta + "'"); return m; }
        m.ferramenta = f.GetComponent<XRGrabInteractable>();
        m.brilhoFerramenta = CriarBrilho(f, brilho);
        return m;
    }

    // Halo (esfera sem collider, material aditivo) em volta da ferramenta. Começa desligado.
    static Renderer CriarBrilho(Transform ferramenta, Material mat)
    {
        Transform existente = ferramenta.Find("Brilho");
        if (existente != null) Undo.DestroyObjectImmediate(existente.gameObject);

        var esfera = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        esfera.name = "Brilho";
        Object.DestroyImmediate(esfera.GetComponent<Collider>());
        Undo.RegisterCreatedObjectUndo(esfera, "Brilho");
        esfera.transform.SetParent(ferramenta, false);

        var box = ferramenta.GetComponent<BoxCollider>();
        Vector3 tam = box != null ? box.size : Vector3.one * 0.15f;
        esfera.transform.localPosition = box != null ? box.center : Vector3.zero;
        esfera.transform.localScale = new Vector3(Mathf.Max(tam.x, 0.08f), Mathf.Max(tam.y, 0.08f), Mathf.Max(tam.z, 0.08f)) * 1.6f;

        var r = esfera.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.enabled = false;
        return r;
    }

    // ================= Painel de diálogo =================

    static TMP_Text textoStatus, textoAviso;

    static DialogoVN CriarPainel(Transform pai, TMP_FontAsset titulo, TMP_FontAsset corpo, Transform cabeca, XROrigin xr)
    {
        Sprite arredondado = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        Sprite circulo = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        var obj = new GameObject("Dialogo Lia", typeof(RectTransform));
        obj.transform.SetParent(pai, false);
        var canvas = obj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = (RectTransform)obj.transform;
        rt.sizeDelta = new Vector2(900, 460);
        rt.localScale = Vector3.one * 0.001f; // 0,9 m x 0,46 m
        if (cabeca != null)
        {
            Vector3 f = Vector3.ProjectOnPlane(cabeca.forward, Vector3.up).normalized;
            rt.position = cabeca.position + f * 1.4f + Vector3.up * -0.3f;
            rt.rotation = Quaternion.LookRotation(f, Vector3.up);
        }

        // Borda laranja + fundo escuro arredondado
        Imagem(obj.transform, "Borda", arredondado, Laranja, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
        Imagem(obj.transform, "Fundo", arredondado, FundoEscuro, Vector2.zero, Vector2.one, new Vector2(6, 6), new Vector2(-6, -6), true);

        // Retrato da Lia (ícone feito só com UI Image)
        var retrato = Imagem(obj.transform, "Retrato", circulo, Laranja, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, Vector2.zero, false);
        Posicionar(retrato, new Vector2(40, -40), new Vector2(150, 150), new Vector2(0, 1));
        var rosto = Imagem(retrato, "Rosto", circulo, new Color(0.96f, 0.78f, 0.62f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, false);
        Posicionar(rosto, new Vector2(0, -6), new Vector2(124, 124), new Vector2(0.5f, 0.5f));
        var bone = Imagem(retrato, "Bone", arredondado, new Color(0.12f, 0.55f, 0.6f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, true);
        Posicionar(bone, new Vector2(0, 46), new Vector2(118, 34), new Vector2(0.5f, 0.5f));
        var aba = Imagem(retrato, "Aba", arredondado, new Color(0.09f, 0.42f, 0.46f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, true);
        Posicionar(aba, new Vector2(18, 30), new Vector2(110, 12), new Vector2(0.5f, 0.5f));
        foreach (float x in new[] { -24f, 24f })
        {
            var olho = Imagem(retrato, "Olho", circulo, new Color(0.15f, 0.12f, 0.12f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, false);
            Posicionar(olho, new Vector2(x, 0), new Vector2(16, 16), new Vector2(0.5f, 0.5f));
        }
        var boca = Imagem(retrato, "Boca", arredondado, new Color(0.75f, 0.3f, 0.3f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, true);
        Posicionar(boca, new Vector2(0, -32), new Vector2(40, 10), new Vector2(0.5f, 0.5f));

        // Nome e cargo
        var nome = Texto(obj.transform, "Nome", "Lia", titulo, 52, Laranja, TextAlignmentOptions.TopLeft);
        Posicionar(nome.transform, new Vector2(215, -30), new Vector2(400, 64), new Vector2(0, 1));
        var cargo = Texto(obj.transform, "Cargo", "Zeladora do SEBRAELab", corpo, 24, new Color(0.7f, 0.72f, 0.8f), TextAlignmentOptions.TopLeft);
        Posicionar(cargo.transform, new Vector2(330, -48), new Vector2(400, 40), new Vector2(0, 1));

        // Fala
        var fala = Texto(obj.transform, "Fala", "", corpo, 28, Color.white, TextAlignmentOptions.TopLeft);
        var falaRt = fala.rectTransform;
        falaRt.anchorMin = new Vector2(0, 0); falaRt.anchorMax = new Vector2(1, 1);
        falaRt.offsetMin = new Vector2(215, 150); falaRt.offsetMax = new Vector2(-36, -98);

        // Linha de status (substitui o painel de checklist) e avisos (substitui o painel de boas-vindas)
        var status = Texto(obj.transform, "Status", "", corpo, 22, new Color(0.85f, 0.87f, 0.93f), TextAlignmentOptions.TopRight);
        Posicionar(status.transform, new Vector2(-36, -40), new Vector2(420, 40), new Vector2(1, 1));
        status.textWrappingMode = TextWrappingModes.NoWrap;
        var aviso = Texto(obj.transform, "Aviso", "", corpo, 24, new Color(1f, 0.85f, 0.3f), TextAlignmentOptions.BottomLeft);
        Posicionar(aviso.transform, new Vector2(215, 112), new Vector2(649, 36), new Vector2(0, 0));
        aviso.textWrappingMode = TextWrappingModes.NoWrap;
        aviso.overflowMode = TextOverflowModes.Ellipsis;
        textoStatus = status;
        textoAviso = aviso;
        fala.textWrappingMode = TextWrappingModes.Normal;

        // Linha com as teclas do simulador
        var teclas = Texto(obj.transform, "Teclas Simulador", "Simulador: segure <b>Espaço</b> + mouse para mover a mão · <b>G</b> para pegar/registrar/apertar", corpo, 19, new Color(0.62f, 0.64f, 0.72f), TextAlignmentOptions.BottomLeft);
        Posicionar(teclas.transform, new Vector2(36, 22), new Vector2(560, 60), new Vector2(0, 0));
        teclas.rectTransform.anchorMin = teclas.rectTransform.anchorMax = new Vector2(0, 0);
        teclas.textWrappingMode = TextWrappingModes.Normal;

        // Botão Continuar (grande): BoxCollider + XR Simple Interactable
        var botao = new GameObject("Botao Continuar", typeof(RectTransform));
        botao.transform.SetParent(obj.transform, false);
        var brt = (RectTransform)botao.transform;
        brt.anchorMin = brt.anchorMax = new Vector2(1, 0);
        brt.pivot = new Vector2(1, 0);
        brt.anchoredPosition = new Vector2(-28, 22);
        brt.sizeDelta = new Vector2(270, 84);
        var fundoBotao = botao.AddComponent<Image>();
        fundoBotao.sprite = arredondado;
        fundoBotao.type = Image.Type.Sliced;
        fundoBotao.pixelsPerUnitMultiplier = 0.35f;
        fundoBotao.color = Laranja;
        var col = botao.AddComponent<BoxCollider>();
        col.size = new Vector3(270, 84, 30);
        col.center = new Vector3(-135, 42, 0);
        botao.AddComponent<XRSimpleInteractable>();
        var textoBotao = Texto(botao.transform, "Texto", "Continuar", titulo, 40, new Color(0.1f, 0.08f, 0.06f), TextAlignmentOptions.Center);
        textoBotao.rectTransform.anchorMin = Vector2.zero; textoBotao.rectTransform.anchorMax = Vector2.one;
        textoBotao.rectTransform.offsetMin = textoBotao.rectTransform.offsetMax = Vector2.zero;

        var d = obj.AddComponent<DialogoVN>();
        d.textoNome = nome;
        d.textoFala = fala;
        d.botao = botao;
        d.fundoBotao = fundoBotao;
        d.textoBotao = textoBotao;
        d.cabeca = cabeca;
        return d;
    }

    static Transform Imagem(Transform pai, string nome, Sprite sprite, Color cor, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax, bool fatiado)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.transform.SetParent(pai, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = oMin; rt.offsetMax = oMax;
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = cor;
        img.raycastTarget = false;
        if (fatiado) { img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 0.35f; }
        return go.transform;
    }

    static void Posicionar(Transform t, Vector2 pos, Vector2 tam, Vector2 ancora)
    {
        var rt = (RectTransform)t;
        rt.anchorMin = rt.anchorMax = ancora;
        rt.pivot = ancora;
        rt.anchoredPosition = pos;
        rt.sizeDelta = tam;
    }

    static TextMeshProUGUI Texto(Transform pai, string nome, string texto, TMP_FontAsset fonte, float tamanho, Color cor, TextAlignmentOptions alinhamento)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.transform.SetParent(pai, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (fonte != null) t.font = fonte;
        t.text = texto;
        t.fontSize = tamanho;
        t.color = cor;
        t.alignment = alinhamento;
        t.raycastTarget = false;
        return t;
    }

    // ================= Seta guia =================

    static void CriarSeta(Transform pai, TMP_FontAsset fonte, out Transform seta, out Transform cone, out TMP_Text texto)
    {
        seta = new GameObject("Seta Guia").transform;
        seta.SetParent(pai, false);

        var coneObj = new GameObject("Cone");
        coneObj.transform.SetParent(seta, false);
        coneObj.AddComponent<MeshFilter>().sharedMesh = MalhaCone();
        var r = coneObj.AddComponent<MeshRenderer>();
        r.sharedMaterial = MaterialSeta();
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        cone = coneObj.transform;

        var rotulo = new GameObject("Rotulo");
        rotulo.transform.SetParent(seta, false);
        rotulo.transform.localPosition = new Vector3(0, 0.45f, 0);
        var tmp = rotulo.AddComponent<TextMeshPro>();
        if (fonte != null) tmp.font = fonte;
        tmp.fontSize = 1.1f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.rectTransform.sizeDelta = new Vector2(2f, 0.3f);
        tmp.text = "";
        texto = tmp;
        seta.gameObject.SetActive(false);
    }

    // Cone com a ponta na origem e a base 0,25 m acima (aponta para baixo)
    static Mesh MalhaCone()
    {
        if (!AssetDatabase.IsValidFolder(PastaMalhas)) AssetDatabase.CreateFolder("Assets/ReparoLab", "Malhas");
        string caminho = PastaMalhas + "/Seta Cone.asset";
        var existente = AssetDatabase.LoadAssetAtPath<Mesh>(caminho);
        if (existente != null) return existente;

        const int lados = 16;
        const float altura = 0.25f, raio = 0.09f;
        var v = new List<Vector3> { Vector3.zero, new Vector3(0, altura, 0) };
        for (int i = 0; i < lados; i++)
        {
            float a = i * Mathf.PI * 2f / lados;
            v.Add(new Vector3(Mathf.Cos(a) * raio, altura, Mathf.Sin(a) * raio));
        }
        var tri = new List<int>();
        for (int i = 0; i < lados; i++)
        {
            int a = 2 + i, b = 2 + (i + 1) % lados;
            tri.AddRange(new[] { 0, b, a });   // lateral
            tri.AddRange(new[] { 1, a, b });   // tampa
        }
        var mesh = new Mesh { name = "Seta Cone" };
        mesh.SetVertices(v);
        mesh.SetTriangles(tri, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, caminho);
        return mesh;
    }

    // ================= Materiais (sem luz nova: tudo Unlit) =================

    static Material MaterialSeta()
    {
        string caminho = PastaMateriais + "/Seta Guia.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (m != null) return m;
        m = new Material(ShaderUnlit());
        m.SetColor("_BaseColor", Laranja);
        m.color = Laranja;
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", (float)CullMode.Off); // dois lados: não depende da ordem dos triângulos
        GarantirPasta();
        AssetDatabase.CreateAsset(m, caminho);
        return m;
    }

    static Material MaterialBrilho()
    {
        string caminho = PastaMateriais + "/Brilho Ferramenta.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (m != null) return m;
        m = new Material(ShaderUnlit());
        Color c = new Color(1f, 0.6f, 0.15f, 0.35f);
        m.SetColor("_BaseColor", c);
        // Transparente aditivo (URP Unlit)
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 2f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.One);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        GarantirPasta();
        AssetDatabase.CreateAsset(m, caminho);
        return m;
    }

    static Shader ShaderUnlit()
    {
        Shader s = Shader.Find("Universal Render Pipeline/Unlit");
        return s != null ? s : Shader.Find("Unlit/Color");
    }

    static void GarantirPasta()
    {
        if (!AssetDatabase.IsValidFolder("Assets/ReparoLab")) AssetDatabase.CreateFolder("Assets", "ReparoLab");
        if (!AssetDatabase.IsValidFolder(PastaMateriais)) AssetDatabase.CreateFolder("Assets/ReparoLab", "Materiais");
    }

    // ================= Fontes TMP =================

    static TMP_FontAsset CriarFonte(string nome)
    {
        string caminho = PastaFontes + "/" + nome + " SDF.asset";
        var existente = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(caminho);
        if (existente != null) return existente;

        var fonte = AssetDatabase.LoadAssetAtPath<Font>(PastaFontes + "/" + nome + ".ttf");
        if (fonte == null) { Debug.LogError("Reparo Lab: fonte não encontrada: " + PastaFontes + "/" + nome + ".ttf"); return null; }

        var fa = TMP_FontAsset.CreateFontAsset(fonte, 48, 5, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
        fa.name = nome + " SDF";
        AssetDatabase.CreateAsset(fa, caminho);
        fa.atlasTextures[0].name = nome + " Atlas";
        AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
        fa.material.name = nome + " Material";
        AssetDatabase.AddObjectToAsset(fa.material, fa);

        // Latin completo: ASCII + Latin-1 + Latin Extended-A + pontuação tipográfica
        var sb = new StringBuilder();
        for (int c = 0x20; c <= 0x7E; c++) sb.Append((char)c);
        for (int c = 0xA0; c <= 0x17F; c++) sb.Append((char)c);
        sb.Append("‘’‚“”„–—…•·€™");
        fa.TryAddCharacters(sb.ToString(), out string faltando);
        if (!string.IsNullOrEmpty(faltando))
            Debug.Log("Reparo Lab: " + nome + " não tem " + faltando.Length + " caracteres (normal para símbolos raros; os acentos do português estão incluídos).");

        // O TMP pode trocar a textura do atlas ao gerar os glifos: garante que a textura final está salva no asset
        foreach (var tex in fa.atlasTextures)
        {
            if (tex == null) continue;
            if (!AssetDatabase.Contains(tex)) { tex.name = nome + " Atlas"; AssetDatabase.AddObjectToAsset(tex, fa); }
            EditorUtility.SetDirty(tex);
        }
        if (fa.atlasTexture != null) fa.material.SetTexture(ShaderUtilities.ID_MainTex, fa.atlasTexture);

        // Atlas fixo: não precisa gerar glifos no Quest durante o jogo
        fa.atlasPopulationMode = AtlasPopulationMode.Static;
        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        return fa;
    }
}
#endif
