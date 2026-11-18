#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// COLOQUE ESTE ARQUIVO DENTRO DE UMA PASTA CHAMADA "Editor" (Assets/Editor/MontarReparoLab.cs).
// Depois, no menu de cima da Unity: Reparo Lab > Montar cena.
// Ele cria tudo na frente do XR Origin. Depois é só arrastar os defeitos para as paredes do SEBRAELab.
public static class MontarReparoLab
{
    const string PastaMateriais = "Assets/ReparoLab/Materiais";
    const int CamadaLampada = 5; // Interaction Layer usada só pela lâmpada nova e pelo soquete

    static Transform raiz;
    static Vector3 origem, frente, direita;

    [MenuItem("Reparo Lab/Montar cena")]
    public static void Montar()
    {
        if (GameObject.Find("ReparoLab") != null &&
            !EditorUtility.DisplayDialog("Reparo Lab", "Já existe um objeto ReparoLab na cena. Criar outro mesmo assim?", "Criar", "Cancelar"))
            return;

        // Referência: posição e direção do jogador
        XROrigin xr = Object.FindAnyObjectByType<XROrigin>();
        if (xr != null)
        {
            origem = xr.transform.position;
            frente = Vector3.ProjectOnPlane(xr.transform.forward, Vector3.up).normalized;
        }
        else
        {
            Debug.LogWarning("Reparo Lab: nenhum XR Origin encontrado. Montando a partir do ponto (0,0,0).");
            origem = Vector3.zero;
            frente = Vector3.forward;
        }
        if (frente == Vector3.zero) frente = Vector3.forward;
        direita = Vector3.Cross(Vector3.up, frente).normalized;

        raiz = new GameObject("ReparoLab").transform;
        raiz.position = origem;
        Undo.RegisterCreatedObjectUndo(raiz.gameObject, "Montar Reparo Lab");

        // ---------- Painéis ----------
        var painelInfo = CriarPainel("Painel Info", P(2.6f, 0f, 1.7f), new Vector2(900, 520), out TMP_Text textoInfo);
        var textoAviso = CriarTextoExtra(painelInfo, "Aviso", new Vector2(0, -330), new Vector2(900, 120), 42, new Color(1f, 0.85f, 0.3f));
        CriarPainel("Painel Checklist", P(2.4f, 1.6f, 1.6f), new Vector2(600, 520), out TMP_Text textoChecklist, 40);
        var painelFinal = CriarPainel("Painel Final", P(2.6f, -1.6f, 1.6f), new Vector2(800, 600), out TMP_Text textoRelatorio, 44);

        // ---------- Defeitos ----------
        Defeito fissura = CriarFissura(P(3.5f, -2.2f, 1.4f));
        Defeito mancha = CriarMancha(P(3.5f, 2.2f, 1.3f));
        Defeito lampada = CriarLampada(P(2.0f, -0.9f, 2.0f), out GameObject soquete);

        // ---------- Mesa e ferramentas ----------
        Vector3 mesaPos = P(1.2f, 0.9f, 0f);
        CriarPrimitivo(PrimitiveType.Cube, "Mesa de Ferramentas", raiz, mesaPos + Vector3.up * 0.4f, new Vector3(1.2f, 0.8f, 0.6f), Cor("Madeira", new Color(0.55f, 0.38f, 0.22f)), true, Rot());

        CriarEspatula(mesaPos + Vector3.up * 0.85f - direita * 0.35f);
        CriarRolo(mesaPos + Vector3.up * 0.88f);
        CriarLampadaNova(mesaPos + Vector3.up * 0.86f + direita * 0.35f);

        // ---------- Botão final ----------
        GameObject botao = CriarBotao(P(1.6f, -0.9f, 0f));

        // ---------- Gerenciador ----------
        var gerObj = new GameObject("GerenciadorJogo");
        gerObj.transform.SetParent(raiz, false);
        var ger = gerObj.AddComponent<GerenciadorJogo>();
        ger.textoInfo = textoInfo;
        ger.textoAviso = textoAviso;
        ger.textoChecklist = textoChecklist;
        ger.textoRelatorio = textoRelatorio;
        ger.painelFinal = painelFinal;
        ger.botaoLiberar = botao;
        ger.fonteAudio = gerObj.AddComponent<AudioSource>();
        ger.fonteAudio.playOnAwake = false;

        // Liga o botão ao gerenciador
        var botaoInteragivel = botao.GetComponent<XRSimpleInteractable>();
        UnityEventTools.AddVoidPersistentListener(botaoInteragivel.selectEntered, ger.LiberarEspaco);

        painelFinal.SetActive(false);
        botao.SetActive(false);

        EditorUtility.SetDirty(ger);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = raiz.gameObject;
        Debug.Log("Reparo Lab montado! Agora arraste Fissura e Mancha para as paredes e a Lâmpada para o trilho de luz. Salve a cena (Ctrl+S).");
    }

    // ================= Defeitos =================

    static Defeito CriarFissura(Vector3 pos)
    {
        var obj = CriarDefeitoBase("Fissura", pos, new Vector3(0.6f, 0.8f, 0.05f));
        var visual = new GameObject("Visual (rachadura)").transform;
        visual.SetParent(obj.transform, false);
        Material preto = Cor("Rachadura", new Color(0.08f, 0.08f, 0.08f));
        // zigue-zague de traços finos
        Vector3[] pontos = { new Vector3(-0.05f, 0.35f), new Vector3(0.06f, 0.15f), new Vector3(-0.04f, -0.02f), new Vector3(0.08f, -0.2f), new Vector3(0.0f, -0.38f) };
        for (int i = 0; i < pontos.Length - 1; i++)
        {
            Vector3 a = pontos[i], b = pontos[i + 1];
            float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            var traco = CriarPrimitivo(PrimitiveType.Cube, "Traco", visual, Vector3.zero, new Vector3(Vector3.Distance(a, b), 0.025f, 0.02f), preto, false, Quaternion.identity);
            traco.transform.localPosition = (a + b) / 2f - new Vector3(0, 0, 0.02f);
            traco.transform.localRotation = Quaternion.Euler(0, 0, ang);
        }
        var d = obj.GetComponent<Defeito>();
        d.nome = "Fissura na parede";
        d.explicacao = "Causa: movimentação da estrutura, variação de calor e umidade.\nPrevenção: inspeção periódica e reparo enquanto a fissura ainda é pequena.";
        d.ferramentaCorreta = "Espatula";
        d.desativarAoReparar = new[] { visual.gameObject };
        return d;
    }

    static Defeito CriarMancha(Vector3 pos)
    {
        var obj = CriarDefeitoBase("Mancha de Infiltracao", pos, new Vector3(0.9f, 0.7f, 0.05f));
        Material marrom = Cor("Mancha", new Color(0.36f, 0.27f, 0.16f));
        var visual = new GameObject("Visual (mancha)").transform;
        visual.SetParent(obj.transform, false);
        Vector3[] blocos = { new Vector3(0, 0, 0), new Vector3(0.18f, 0.1f, 0), new Vector3(-0.2f, -0.08f, 0), new Vector3(0.05f, -0.2f, 0) };
        float[] tamanhos = { 0.5f, 0.32f, 0.3f, 0.25f };
        for (int i = 0; i < blocos.Length; i++)
        {
            var disco = CriarPrimitivo(PrimitiveType.Cylinder, "Mancha", visual, Vector3.zero, new Vector3(tamanhos[i], 0.005f, tamanhos[i]), marrom, false, Quaternion.identity);
            disco.transform.localPosition = blocos[i] - new Vector3(0, 0, 0.02f - i * 0.001f);
            disco.transform.localRotation = Quaternion.Euler(90, 0, 0);
        }
        var d = obj.GetComponent<Defeito>();
        d.nome = "Mancha de infiltração";
        d.explicacao = "Causa: água vinda do telhado, da calha ou de tubulações.\nPrevenção: revisar calhas antes das chuvas e resolver a origem antes de pintar.";
        d.ferramentaCorreta = "Rolo";
        d.desativarAoReparar = new[] { visual.gameObject };
        return d;
    }

    static Defeito CriarLampada(Vector3 pos, out GameObject soquete)
    {
        var obj = new GameObject("Lampada Queimada");
        obj.transform.SetParent(raiz, false);
        obj.transform.SetPositionAndRotation(pos, Rot());
        var col = obj.AddComponent<SphereCollider>();
        col.radius = 0.12f;

        var apagada = CriarPrimitivo(PrimitiveType.Sphere, "Lampada Apagada", obj.transform, Vector3.zero, Vector3.one * 0.12f, Cor("Lampada Apagada", new Color(0.3f, 0.3f, 0.3f)), false, Quaternion.identity);
        apagada.transform.localPosition = Vector3.zero;
        CriarPrimitivo(PrimitiveType.Cylinder, "Suporte", obj.transform, Vector3.zero, new Vector3(0.06f, 0.08f, 0.06f), Cor("Metal", new Color(0.2f, 0.2f, 0.22f)), false, Quaternion.identity).transform.localPosition = new Vector3(0, 0.12f, 0);

        // Luz acesa (começa desligada)
        var acesa = new GameObject("Luz Acesa");
        acesa.transform.SetParent(obj.transform, false);
        var esferaAcesa = CriarPrimitivo(PrimitiveType.Sphere, "Lampada Acesa", acesa.transform, Vector3.zero, Vector3.one * 0.13f, Emissivo("Lampada Acesa", new Color(1f, 0.9f, 0.5f)), false, Quaternion.identity);
        esferaAcesa.transform.localPosition = Vector3.zero;
        var luz = acesa.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.range = 4f;
        luz.intensity = 3f;
        luz.color = new Color(1f, 0.92f, 0.7f);
        acesa.SetActive(false);

        obj.AddComponent<XRSimpleInteractable>();
        var d = obj.AddComponent<Defeito>();
        d.nome = "Lâmpada queimada";
        d.explicacao = "Causa: fim da vida útil ou oscilação na rede elétrica.\nPrevenção: usar lâmpadas LED e revisar a instalação elétrica.\n\nPegue a lâmpada nova na mesa e encaixe no suporte.";
        d.ferramentaCorreta = ""; // conserta por encaixe
        d.ativarAoReparar = new[] { acesa };
        d.desativarAoReparar = new[] { apagada };
        UnityEventTools.AddVoidPersistentListener(obj.GetComponent<XRSimpleInteractable>().selectEntered, d.Identificar);

        // Soquete: só aceita a lâmpada nova (Interaction Layer 5)
        soquete = new GameObject("Soquete");
        soquete.transform.SetParent(obj.transform, false);
        soquete.transform.localPosition = new Vector3(0, -0.02f, 0);
        var sc = soquete.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = 0.3f; // um pouco maior: facilita encaixar segurando a lâmpada pelo raio
        var socket = soquete.AddComponent<XRSocketInteractor>();
        socket.interactionLayers = 1 << CamadaLampada;
        UnityEventTools.AddVoidPersistentListener(socket.selectEntered, d.Reparar);
        return d;
    }

    static GameObject CriarDefeitoBase(string nome, Vector3 pos, Vector3 tamanho)
    {
        var obj = new GameObject(nome);
        obj.transform.SetParent(raiz, false);
        obj.transform.SetPositionAndRotation(pos, Rot());

        // Collider normal: o raio do controle acerta este para registrar
        var solido = obj.AddComponent<BoxCollider>();
        solido.size = tamanho;

        // Collider gatilho: a ferramenta encosta aqui para consertar
        var gatilho = obj.AddComponent<BoxCollider>();
        gatilho.isTrigger = true;
        gatilho.size = new Vector3(tamanho.x + 0.1f, tamanho.y + 0.1f, 0.5f);

        var interagivel = obj.AddComponent<XRSimpleInteractable>();
        var d = obj.AddComponent<Defeito>();
        UnityEventTools.AddVoidPersistentListener(interagivel.selectEntered, d.Identificar);
        return obj;
    }

    // ================= Ferramentas =================

    static void CriarEspatula(Vector3 pos)
    {
        var obj = CriarPegavel("Espatula", pos, new Vector3(0.12f, 0.04f, 0.32f));
        CriarPrimitivo(PrimitiveType.Cube, "Lamina", obj.transform, Vector3.zero, new Vector3(0.12f, 0.005f, 0.12f), Cor("Metal Claro", new Color(0.75f, 0.77f, 0.8f)), false, Quaternion.identity).transform.localPosition = new Vector3(0, 0, 0.1f);
        var cabo = CriarPrimitivo(PrimitiveType.Cylinder, "Cabo", obj.transform, Vector3.zero, new Vector3(0.03f, 0.1f, 0.03f), Cor("Cabo Vermelho", new Color(0.75f, 0.15f, 0.15f)), false, Quaternion.identity);
        cabo.transform.localPosition = new Vector3(0, 0, -0.06f);
        cabo.transform.localRotation = Quaternion.Euler(90, 0, 0);
        obj.AddComponent<Ferramenta>().tipo = "Espatula";
    }

    static void CriarRolo(Vector3 pos)
    {
        var obj = CriarPegavel("Rolo de Tinta", pos, new Vector3(0.22f, 0.08f, 0.35f));
        var rolo = CriarPrimitivo(PrimitiveType.Cylinder, "Rolo", obj.transform, Vector3.zero, new Vector3(0.07f, 0.1f, 0.07f), Cor("Rolo Branco", new Color(0.95f, 0.95f, 0.9f)), false, Quaternion.identity);
        rolo.transform.localPosition = new Vector3(0, 0, 0.12f);
        rolo.transform.localRotation = Quaternion.Euler(0, 0, 90);
        var cabo = CriarPrimitivo(PrimitiveType.Cylinder, "Cabo", obj.transform, Vector3.zero, new Vector3(0.03f, 0.12f, 0.03f), Cor("Cabo Azul", new Color(0.15f, 0.35f, 0.75f)), false, Quaternion.identity);
        cabo.transform.localPosition = new Vector3(0, 0, -0.05f);
        cabo.transform.localRotation = Quaternion.Euler(90, 0, 0);
        obj.AddComponent<Ferramenta>().tipo = "Rolo";
    }

    static void CriarLampadaNova(Vector3 pos)
    {
        var obj = CriarPegavel("Lampada Nova", pos, new Vector3(0.1f, 0.12f, 0.1f));
        var vidro = CriarPrimitivo(PrimitiveType.Sphere, "Vidro", obj.transform, Vector3.zero, Vector3.one * 0.1f, Emissivo("Lampada Nova", new Color(1f, 0.95f, 0.7f) * 0.4f), false, Quaternion.identity);
        vidro.transform.localPosition = Vector3.zero;
        // Pode ser pega pelas mãos (camada Default) e encaixada no soquete (camada 5)
        obj.GetComponent<XRGrabInteractable>().interactionLayers = 1 | (1 << CamadaLampada);
    }

    static GameObject CriarPegavel(string nome, Vector3 pos, Vector3 tamanhoCollider)
    {
        var obj = new GameObject(nome);
        obj.transform.SetParent(raiz, false);
        obj.transform.SetPositionAndRotation(pos, Rot());
        var col = obj.AddComponent<BoxCollider>();
        col.size = tamanhoCollider;
        var rb = obj.AddComponent<Rigidbody>();
        rb.mass = 0.5f;
        var grab = obj.AddComponent<XRGrabInteractable>();
        grab.throwOnDetach = false;
        return obj;
    }

    // ================= Botão e painéis =================

    static GameObject CriarBotao(Vector3 pos)
    {
        var obj = new GameObject("Botao Liberar Espaco");
        obj.transform.SetParent(raiz, false);
        obj.transform.SetPositionAndRotation(pos, Rot());
        CriarPrimitivo(PrimitiveType.Cylinder, "Pedestal", obj.transform, Vector3.zero, new Vector3(0.3f, 0.5f, 0.3f), Cor("Metal", new Color(0.2f, 0.2f, 0.22f)), false, Quaternion.identity).transform.localPosition = new Vector3(0, 0.5f, 0);
        var botao = CriarPrimitivo(PrimitiveType.Cylinder, "Botao", obj.transform, Vector3.zero, new Vector3(0.22f, 0.04f, 0.22f), Emissivo("Botao Vermelho", new Color(0.9f, 0.1f, 0.1f) * 0.6f), false, Quaternion.identity);
        botao.transform.localPosition = new Vector3(0, 1.03f, 0);

        var col = obj.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 0.9f, 0);
        col.size = new Vector3(0.35f, 0.3f, 0.35f);
        obj.AddComponent<XRSimpleInteractable>();

        var rotulo = new GameObject("Rotulo");
        rotulo.transform.SetParent(obj.transform, false);
        rotulo.transform.localPosition = new Vector3(0, 1.3f, 0);
        var tmp = rotulo.AddComponent<TextMeshPro>();
        tmp.text = "LIBERAR\nESPAÇO";
        tmp.fontSize = 1.2f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.rectTransform.sizeDelta = new Vector2(1f, 0.5f);
        return obj;
    }

    static GameObject CriarPainel(string nome, Vector3 pos, Vector2 tamanho, out TMP_Text texto, float fonte = 46)
    {
        var obj = new GameObject(nome, typeof(RectTransform));
        obj.transform.SetParent(raiz, false);
        obj.transform.SetPositionAndRotation(pos, Rot());
        var canvas = obj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = obj.GetComponent<RectTransform>();
        rt.sizeDelta = tamanho;
        rt.localScale = Vector3.one * 0.002f;
        var fundo = obj.AddComponent<Image>();
        fundo.color = new Color(0.08f, 0.1f, 0.18f, 0.92f);

        var t = new GameObject("Texto", typeof(RectTransform));
        t.transform.SetParent(obj.transform, false);
        var trt = t.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(40, 30);
        trt.offsetMax = new Vector2(-40, -30);
        var tmp = t.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fonte;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.text = nome;
        texto = tmp;
        return obj;
    }

    static TMP_Text CriarTextoExtra(GameObject painel, string nome, Vector2 posicao, Vector2 tamanho, float fonte, Color cor)
    {
        var t = new GameObject(nome, typeof(RectTransform));
        t.transform.SetParent(painel.transform, false);
        var rt = t.GetComponent<RectTransform>();
        rt.anchoredPosition = posicao;
        rt.sizeDelta = tamanho;
        var tmp = t.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fonte;
        tmp.color = cor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.text = "";
        return tmp;
    }

    // ================= Utilitários =================

    // Posição relativa ao jogador: distância à frente, deslocamento lateral, altura
    static Vector3 P(float aFrente, float aDireita, float altura)
    {
        return origem + frente * aFrente + direita * aDireita + Vector3.up * altura;
    }

    // Rotação virada para o lado oposto ao jogador (painéis e textos ficam legíveis)
    static Quaternion Rot() => Quaternion.LookRotation(frente, Vector3.up);

    static GameObject CriarPrimitivo(PrimitiveType tipo, string nome, Transform pai, Vector3 posMundo, Vector3 escala, Material mat, bool manterCollider, Quaternion rot)
    {
        var obj = GameObject.CreatePrimitive(tipo);
        obj.name = nome;
        obj.transform.SetParent(pai, false);
        obj.transform.SetPositionAndRotation(posMundo, rot);
        obj.transform.localScale = escala;
        if (!manterCollider) Object.DestroyImmediate(obj.GetComponent<Collider>());
        if (mat != null) obj.GetComponent<Renderer>().sharedMaterial = mat;
        return obj;
    }

    static Material Cor(string nome, Color cor) => CriarMaterial(nome, cor, false);
    static Material Emissivo(string nome, Color cor) => CriarMaterial(nome, cor, true);

    static Material CriarMaterial(string nome, Color cor, bool emissivo)
    {
        if (!AssetDatabase.IsValidFolder("Assets/ReparoLab")) AssetDatabase.CreateFolder("Assets", "ReparoLab");
        if (!AssetDatabase.IsValidFolder(PastaMateriais)) AssetDatabase.CreateFolder("Assets/ReparoLab", "Materiais");

        string caminho = PastaMateriais + "/" + nome + ".mat";
        var existente = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (existente != null) return existente;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var mat = new Material(shader);
        mat.color = cor;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", cor);
        if (emissivo)
        {
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetColor("_EmissionColor", cor * 2f);
        }
        AssetDatabase.CreateAsset(mat, caminho);
        return mat;
    }
}
#endif
