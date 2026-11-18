using System.Collections;
using UnityEngine;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.Interactables; // XR Interaction Toolkit 3.x

// Roteiro guiado da Lia: avança sozinho conforme o jogador age.
// Abertura -> teleporte -> (registrar -> pegar ferramenta -> levar ao defeito) x3 -> liberar espaço -> relatório.
// Guias: seta flutuante no alvo atual, defeito pulsando, ferramenta certa brilhando (mais forte perto do defeito).
public class GuiaTutorial : MonoBehaviour
{
    [System.Serializable]
    public class Missao
    {
        public Defeito defeito;
        public Transform visualDefeito;          // parte que pulsa
        public Transform pontoReparo;            // onde a ferramenta encosta (o defeito ou o soquete)
        public XRGrabInteractable ferramenta;
        public Renderer brilhoFerramenta;        // halo da ferramenta (começa desligado)
        public string nomeDefeito = "a rachadura";
        public string nomeFerramenta = "a espátula";
    }

    [Header("Referências")]
    public DialogoVN dialogo;
    public GerenciadorJogo gerenciador;
    public XROrigin xrOrigin;
    public Transform cabeca;
    public Transform botaoLiberar;
    public Missao[] missoes;

    [Header("Seta guia")]
    public Transform seta;            // pai da seta
    public Transform cone;            // malha do cone (ponta para baixo)
    public TMP_Text textoSeta;

    [Header("Brilho")]
    public Color corBrilho = new Color(1f, 0.6f, 0.15f, 1f);

    [Header("Registrar olhando")]
    public float tempoOlhar = 1f;
    public float anguloOlhar = 10f;
    public float distanciaOlhar = 14f;

    // estado dos guias
    Transform alvo;
    Bounds limitesAlvo;
    Transform pulsando;
    Vector3 escalaPulso;
    Missao missaoAtual;
    bool olharAtivo;
    Defeito olhado;
    float tempoOlhando;
    MaterialPropertyBlock bloco;
    Vector3 escalaBrilho;

    static readonly int IdCor = Shader.PropertyToID("_BaseColor");

    void Start()
    {
        bloco = new MaterialPropertyBlock();
        if (cabeca == null && xrOrigin != null && xrOrigin.Camera != null) cabeca = xrOrigin.Camera.transform;
        if (cabeca == null && Camera.main != null) cabeca = Camera.main.transform;
        foreach (var m in missoes)
            if (m.brilhoFerramenta != null) m.brilhoFerramenta.enabled = false;
        Apontar(null, "");
        StartCoroutine(Roteiro());
    }

    // ===================== Roteiro =====================

    IEnumerator Roteiro()
    {
        yield return null; // deixa o GerenciadorJogo iniciar

        yield return Fala("Oi! Eu sou a <b>Lia</b>, zeladora do SEBRAELab. Que bom que você chegou!", "Continuar");
        yield return Fala("Hoje à noite tem evento aqui, mas o salão está com problemas. Me ajuda a deixar tudo pronto?", "Continuar");
        yield return Fala("São 3 defeitos. Para cada um: <b>registre</b>, pegue a <b>ferramenta certa</b> na mesa e <b>conserte</b>. No final, a gente libera o espaço!", "Bora!");

        // ---------- Teleporte ----------
        Vector3 inicio = xrOrigin != null ? xrOrigin.transform.position : Vector3.zero;
        Missao primeira = missoes.Length > 0 ? missoes[0] : null;
        if (xrOrigin != null && primeira != null && primeira.defeito != null)
        {
            Transform marca = PontoNoChao(primeira.defeito.transform, inicio.y);
            Apontar(marca, "Teleporte aqui");
            dialogo.Falar("Primeiro, chegue mais perto da parede. Mire o raio de teleporte no chão, perto da seta, e solte.");
            float t0 = Time.time;
            while (DistanciaPlana(xrOrigin.transform.position, inicio) < 1f)
            {
                if (Time.time - t0 > 15f && !dialogo.BotaoVisivel) dialogo.MostrarBotao("Pular");
                if (dialogo.ConsumirContinuar()) break;
                yield return null;
            }
            Destroy(marca.gameObject);
            Apontar(null, "");
            yield return FalaTempo("Isso! Agora você se move pelo salão assim sempre que precisar.", 2.5f);
        }

        olharAtivo = true;

        // ---------- Missões ----------
        for (int i = 0; i < missoes.Length; i++)
        {
            Missao m = missoes[i];
            if (m.defeito == null || m.defeito.estado == Defeito.Estado.Reparado) continue;
            bool detalhado = i == 0;
            missaoAtual = m;
            Pulsar(m.visualDefeito != null ? m.visualDefeito : m.defeito.transform);

            // 1) Registrar
            if (m.defeito.estado == Defeito.Estado.Intacto)
            {
                Apontar(m.defeito.transform, detalhado ? "Olhe aqui por 1 s" : "Registrar");
                if (detalhado)
                    dialogo.Falar("Está vendo aquela <b>rachadura</b> na parede, onde está a seta? <b>Olhe fixo para ela por 1 segundo</b> para registrar. (Também vale apontar o raio e apertar G.)");
                else if (i == 1)
                    dialogo.Falar("Próximo: a <b>mancha de infiltração</b>. Você já sabe: olhe para ela para registrar.");
                else
                    dialogo.Falar("Por último, a <b>lâmpada queimada</b> lá no teto. Registre ela também!");
                while (m.defeito.estado == Defeito.Estado.Intacto) yield return null;
            }

            // 2) Pegar a ferramenta / 3) Levar até o defeito
            Brilhar(m, true);
            bool falouPegar = false, falouLevar = false, explicou = false;
            while (m.defeito.estado != Defeito.Estado.Reparado)
            {
                bool segurando = m.ferramenta != null && m.ferramenta.isSelected;
                if (!segurando && !falouPegar)
                {
                    falouPegar = true; falouLevar = false;
                    Apontar(m.ferramenta != null ? m.ferramenta.transform : null, "Pegue " + m.nomeFerramenta);
                    if (!explicou)
                    {
                        // Ficha do defeito (causa e prevenção) + próxima ação
                        explicou = true;
                        dialogo.Falar("<b>" + m.defeito.nome + "</b> registrada!\n<size=82%><color=#C9CCDA>" + m.defeito.explicacao + "</color></size>\n" +
                            (detalhado ? "Agora pegue <b>" + m.nomeFerramenta + "</b> na mesa (é a que brilha): aponte o raio e segure G."
                                       : "Agora pegue <b>" + m.nomeFerramenta + "</b> na mesa."));
                    }
                    else
                        dialogo.Falar("Soltou? Sem problema: ela fica parada no ar e depois volta para a mesa. Pegue <b>" + m.nomeFerramenta + "</b> de novo.");
                }
                else if (segurando && !falouLevar)
                {
                    falouLevar = true; falouPegar = false;
                    Apontar(m.pontoReparo != null ? m.pontoReparo : m.defeito.transform, "Leve até " + m.nomeDefeito);
                    dialogo.Falar(detalhado
                        ? "Isso! <b>Leve até " + m.nomeDefeito + "</b> e encoste nela. Quanto mais perto, mais forte o brilho."
                        : "Leve até " + m.nomeDefeito + ".");
                }
                yield return null;
            }

            Brilhar(m, false);
            Pulsar(null);
            Apontar(null, "");
            missaoAtual = null;
            int faltam = Faltam();
            if (faltam > 0)
                yield return FalaTempo(detalhado
                    ? "Perfeito, ficou novinha! Agora você já pegou o jeito. Faltam " + faltam + "."
                    : "Mandou bem! Falta" + (faltam > 1 ? "m " : " ") + faltam + ".", 2.5f);
        }

        // ---------- Final ----------
        olharAtivo = false;
        Apontar(botaoLiberar, "Aperte!");
        dialogo.Falar("Tudo consertado! Agora aperte o botão vermelho <b>LIBERAR ESPAÇO</b>: aponte o raio e aperte G.");
        while (gerenciador != null && !gerenciador.Terminou) yield return null;
        Apontar(null, "");

        float tempo = gerenciador != null ? gerenciador.TempoFinal : 0f;
        int erros = gerenciador != null ? gerenciador.Erros : 0;
        string sobreErros = erros == 0 ? "sem nenhum erro de ferramenta. Impecável!" : "com " + erros + " erro" + (erros > 1 ? "s" : "") + " de ferramenta.";
        yield return Fala("Espaço liberado! Você levou <b>" + Mathf.FloorToInt(tempo / 60f) + "min " + Mathf.FloorToInt(tempo % 60f).ToString("00") + "s</b>, " + sobreErros +
                          "\nO relatório completo está no painel. Lembre: conservar é mais barato que consertar!", "Jogar de novo");
        if (gerenciador != null) gerenciador.Reiniciar();
    }

    IEnumerator Fala(string texto, string botao)
    {
        dialogo.Falar(texto, botao);
        while (!dialogo.ConsumirContinuar()) yield return null;
    }

    IEnumerator FalaTempo(string texto, float segundosDepoisDeDigitar)
    {
        dialogo.Falar(texto);
        while (dialogo.Digitando) yield return null;
        yield return new WaitForSeconds(segundosDepoisDeDigitar);
    }

    int Faltam()
    {
        int n = 0;
        foreach (var m in missoes) if (m.defeito != null && m.defeito.estado != Defeito.Estado.Reparado) n++;
        return n;
    }

    // ===================== Guias visuais =====================

    void Update()
    {
        AtualizarSeta();
        float proximidade = Proximidade();
        AtualizarPulso(proximidade);
        AtualizarBrilho(proximidade);
        RegistrarPeloOlhar();
    }

    string rotuloSeta = "";
    int progressoMostrado = -1;

    void Apontar(Transform novoAlvo, string texto)
    {
        rotuloSeta = texto;
        progressoMostrado = -1;
        alvo = novoAlvo;
        if (seta != null) seta.gameObject.SetActive(alvo != null);
        MostrarRotulo(texto);
        if (alvo != null) limitesAlvo = Limites(alvo);
    }

    void MostrarRotulo(string texto)
    {
        if (textoSeta != null) textoSeta.text = string.IsNullOrEmpty(texto) ? "" : "<mark=#101522CC padding=\"12,12,6,6\">" + texto + "</mark>";
    }

    void AtualizarSeta()
    {
        if (seta == null || alvo == null) return;
        limitesAlvo = Limites(alvo); // o alvo pode se mexer (ferramenta na mão)
        float olhoY = cabeca != null ? cabeca.position.y : limitesAlvo.center.y;
        bool alvoNoAlto = limitesAlvo.min.y > olhoY + 0.4f;   // lâmpada no teto: seta embaixo, apontando para cima
        float balanco = Mathf.Sin(Time.time * 3f) * 0.06f;

        Vector3 pos = alvoNoAlto
            ? new Vector3(limitesAlvo.center.x, limitesAlvo.min.y - 0.25f - balanco, limitesAlvo.center.z)
            : new Vector3(limitesAlvo.center.x, limitesAlvo.max.y + 0.08f + balanco, limitesAlvo.center.z);
        seta.position = pos;
        if (cone != null)
        {
            cone.localRotation = (alvoNoAlto ? Quaternion.Euler(180f, 0f, 0f) : Quaternion.identity) * Quaternion.Euler(0f, Time.time * 90f, 0f);
            cone.localPosition = Vector3.zero;
        }
        if (textoSeta != null)
        {
            textoSeta.transform.localPosition = new Vector3(0f, alvoNoAlto ? -0.42f : 0.45f, 0f);
            if (cabeca != null)
            {
                Vector3 olhar = textoSeta.transform.position - cabeca.position;
                if (olhar.sqrMagnitude > 0.001f) textoSeta.transform.rotation = Quaternion.LookRotation(olhar, Vector3.up);
            }
        }
    }

    void Pulsar(Transform t)
    {
        if (pulsando != null) pulsando.localScale = escalaPulso;
        pulsando = t;
        if (pulsando != null) escalaPulso = pulsando.localScale;
    }

    void AtualizarPulso(float proximidade)
    {
        if (pulsando == null) return;
        bool sendoOlhado = olhado != null && missaoAtual != null && olhado == missaoAtual.defeito;
        float amplitude = 0.07f + 0.1f * proximidade + (sendoOlhado ? 0.06f : 0f);
        float frequencia = 4f + 6f * proximidade + (sendoOlhado ? 6f : 0f);
        pulsando.localScale = escalaPulso * (1f + amplitude * (0.5f + 0.5f * Mathf.Sin(Time.time * frequencia)));
    }

    void Brilhar(Missao m, bool ligar)
    {
        if (m == null || m.brilhoFerramenta == null) return;
        m.brilhoFerramenta.enabled = ligar;
        if (ligar) escalaBrilho = m.brilhoFerramenta.transform.localScale;
        else m.brilhoFerramenta.transform.localScale = escalaBrilho;
    }

    void AtualizarBrilho(float proximidade)
    {
        if (missaoAtual == null || missaoAtual.brilhoFerramenta == null || !missaoAtual.brilhoFerramenta.enabled) return;
        Renderer r = missaoAtual.brilhoFerramenta;
        float pulso = 0.5f + 0.5f * Mathf.Sin(Time.time * (4f + 8f * proximidade));
        Color c = corBrilho;
        c.a = Mathf.Lerp(0.25f, 0.45f, pulso) + 0.5f * proximidade;
        r.GetPropertyBlock(bloco);
        bloco.SetColor(IdCor, c);
        r.SetPropertyBlock(bloco);
        r.transform.localScale = escalaBrilho * (1f + 0.35f * proximidade + 0.05f * pulso);
    }

    // 0 = longe, 1 = encostando (só conta com a ferramenta na mão)
    float Proximidade()
    {
        var m = missaoAtual;
        if (m == null || m.ferramenta == null || !m.ferramenta.isSelected || m.defeito.estado != Defeito.Estado.Identificado) return 0f;
        Transform ponto = m.pontoReparo != null ? m.pontoReparo : m.defeito.transform;
        float d = Vector3.Distance(m.ferramenta.transform.position, ponto.position);
        return 1f - Mathf.Clamp01((d - 0.2f) / 1.3f);
    }

    void RegistrarPeloOlhar()
    {
        if (!olharAtivo || cabeca == null) { olhado = null; tempoOlhando = 0f; return; }
        Defeito melhor = null;
        float melhorAngulo = float.MaxValue;
        foreach (var d in Defeito.Todos)
        {
            if (d == null || d.estado != Defeito.Estado.Intacto) continue;
            Vector3 para = d.transform.position - cabeca.position;
            if (para.magnitude > distanciaOlhar) continue;
            float ang = Vector3.Angle(cabeca.forward, para);
            bool atual = missaoAtual != null && d == missaoAtual.defeito;
            float tolerancia = anguloOlhar;
            if (atual)
            {
                tolerancia += 8f; // o defeito da missão atual é mais fácil de registrar (ex.: lâmpada no teto)
                if (seta != null && seta.gameObject.activeSelf)
                    ang = Mathf.Min(ang, Vector3.Angle(cabeca.forward, seta.position - cabeca.position)); // olhar a seta também vale
            }
            if (ang < tolerancia && ang < melhorAngulo) { melhorAngulo = ang; melhor = d; }
        }
        if (melhor != olhado) { olhado = melhor; tempoOlhando = 0f; }

        // Mostra o progresso na seta enquanto olha o alvo atual
        bool noAlvo = olhado != null && missaoAtual != null && olhado == missaoAtual.defeito;
        int progresso = noAlvo ? Mathf.Clamp(Mathf.RoundToInt(100f * tempoOlhando / tempoOlhar), 0, 100) : -1;
        if (progresso != progressoMostrado)
        {
            progressoMostrado = progresso;
            MostrarRotulo(progresso >= 0 ? "Registrando... " + progresso + "%" : rotuloSeta);
        }

        if (olhado == null) return;
        tempoOlhando += Time.deltaTime;
        if (tempoOlhando >= tempoOlhar) { olhado.Identificar(); olhado = null; tempoOlhando = 0f; }
    }

    // ===================== Utilitários =====================

    Transform PontoNoChao(Transform defeito, float chaoY)
    {
        // 2 m à frente da parede do defeito, na altura do chão
        Vector3 p = defeito.position - defeito.forward * 2f;
        p.y = chaoY;
        var marca = new GameObject("Marca Teleporte").transform;
        marca.position = p;
        return marca;
    }

    static float DistanciaPlana(Vector3 a, Vector3 b) { a.y = 0; b.y = 0; return Vector3.Distance(a, b); }

    static Bounds Limites(Transform t)
    {
        var rs = t.GetComponentsInChildren<Renderer>();
        Bounds b = new Bounds(t.position, Vector3.zero);
        bool primeiro = true;
        foreach (var r in rs)
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy || r is ParticleSystemRenderer) continue;
            if (primeiro) { b = r.bounds; primeiro = false; } else b.Encapsulate(r.bounds);
        }
        return b;
    }
}
