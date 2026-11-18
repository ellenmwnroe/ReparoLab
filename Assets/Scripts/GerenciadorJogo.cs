using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// Coloque este script num objeto vazio chamado "GerenciadorJogo".
// Ele controla o checklist, o tempo, os erros, o painel de informação e o fim da experiência.
public class GerenciadorJogo : MonoBehaviour
{
    public static GerenciadorJogo Instancia { get; private set; }

    [Header("Textos dos painéis (TextMeshPro)")]
    public TMP_Text textoChecklist;   // painel que mostra "0 de 3 reparados"
    public TMP_Text textoInfo;        // painel com o nome e a explicação do defeito
    public TMP_Text textoAviso;       // mensagens rápidas (erros, dicas)
    public TMP_Text textoRelatorio;   // texto do relatório final

    [Tooltip("Mostra o checklist numa linha só (usado no painel da Lia)")]
    public bool checklistCompacto = false;

    [Header("Objetos")]
    public GameObject painelFinal;          // painel do relatório, começa DESATIVADO
    public GameObject botaoLiberar;         // botão "Liberar espaço", começa DESATIVADO
    public GameObject[] ativarNoFinal;      // efeito de "pronto para o evento" (opcional)

    [Header("Sons (opcional)")]
    public AudioSource fonteAudio;
    public AudioClip somVitoria;

    [Header("Texto final")]
    [TextArea(2, 4)]
    public string dicaFinal = "Dica: conservar é mais barato que consertar. Inspeções regulares evitam que pequenos danos virem grandes reformas.";

    // Lidos pelo tutorial da Lia (GuiaTutorial)
    public bool Terminou => terminou;
    public int Erros => erros;
    public float TempoFinal { get; private set; }

    Defeito[] defeitos;
    int erros;
    float inicio;
    bool terminou;
    float limparAvisoEm;

    void Awake()
    {
        Instancia = this;
    }

    void Start()
    {
        defeitos = Defeito.Todos.ToArray();
        inicio = Time.time;

        if (painelFinal != null) painelFinal.SetActive(false);
        if (botaoLiberar != null) botaoLiberar.SetActive(false);

        if (textoInfo != null)
            textoInfo.text = "Bem-vindo, guardião do SEBRAELab!\n\nO evento começa em breve. Aponte o raio para os problemas do salão e aperte o GRIP (botão lateral) para registrar. Depois pegue a ferramenta certa na mesa (também com o GRIP) e encoste no defeito.";
        if (textoAviso != null) textoAviso.text = "";

        AtualizarChecklist();
    }

    void Update()
    {
        if (textoAviso != null && limparAvisoEm > 0 && Time.time > limparAvisoEm)
        {
            textoAviso.text = "";
            limparAvisoEm = 0;
        }

        if (!terminou) AtualizarChecklist();
    }

    public void DefeitoIdentificado(Defeito d)
    {
        if (textoInfo != null)
            textoInfo.text = "<b>" + d.nome + "</b>\n\n" + d.explicacao;
        Aviso("Defeito registrado! Pegue a ferramenta certa na mesa.");
    }

    public void DefeitoReparado(Defeito d)
    {
        Aviso(d.nome + " reparado!");
        AtualizarChecklist();

        if (Reparados() >= defeitos.Length && botaoLiberar != null)
        {
            botaoLiberar.SetActive(true);
            Aviso("Tudo pronto! Aperte o botão LIBERAR ESPAÇO.");
        }
    }

    public void RegistrarErro(string mensagem)
    {
        erros++;
        Aviso("Ferramenta errada! " + mensagem);
        AtualizarChecklist();
    }

    public void Aviso(string mensagem)
    {
        if (textoAviso == null) return;
        textoAviso.text = mensagem;
        limparAvisoEm = Time.time + 3f;
    }

    // Ligue em: botão "Liberar espaço" > XR Simple Interactable > Select Entered
    public void LiberarEspaco()
    {
        if (terminou) { Reiniciar(); return; }       // apertar de novo recomeça
        if (Reparados() < defeitos.Length) return;

        terminou = true;
        float tempo = Time.time - inicio;
        TempoFinal = tempo;
        int min = Mathf.FloorToInt(tempo / 60f);
        int seg = Mathf.FloorToInt(tempo % 60f);

        if (painelFinal != null) painelFinal.SetActive(true);
        if (textoRelatorio != null)
            textoRelatorio.text =
                "<b>ESPAÇO LIBERADO!</b>\n\n" +
                "Defeitos reparados: " + Reparados() + " de " + defeitos.Length + "\n" +
                "Tempo: " + min + "min " + seg.ToString("00") + "s\n" +
                "Erros de ferramenta: " + erros + "\n\n" +
                dicaFinal + "\n\n" +
                "<size=70%>Aperte o botão de novo para recomeçar.</size>";

        if (ativarNoFinal != null)
            foreach (GameObject obj in ativarNoFinal)
                if (obj != null) obj.SetActive(true);

        if (fonteAudio != null && somVitoria != null) fonteAudio.PlayOneShot(somVitoria);
    }

    public void Reiniciar()
    {
        // Recarrega pelo caminho: funciona mesmo se o índice na Scene List mudar
        SceneManager.LoadScene(SceneManager.GetActiveScene().path);
    }

    int Reparados()
    {
        int n = 0;
        foreach (Defeito d in defeitos)
            if (d.estado == Defeito.Estado.Reparado) n++;
        return n;
    }

    void AtualizarChecklist()
    {
        if (textoChecklist == null || defeitos == null) return;

        float tempo = Time.time - inicio;
        if (checklistCompacto)
        {
            textoChecklist.text = "Consertados <b>" + Reparados() + "/" + defeitos.Length + "</b>    Tempo <b>" +
                Mathf.FloorToInt(tempo / 60f) + ":" + Mathf.FloorToInt(tempo % 60f).ToString("00") + "</b>    Erros <b>" + erros + "</b>";
            return;
        }
        string linhas = "<b>CHECKLIST</b>\n";
        foreach (Defeito d in defeitos)
        {
            string marca = d.estado == Defeito.Estado.Reparado ? "[OK]" :
                           d.estado == Defeito.Estado.Identificado ? "[ ! ]" : "[   ]";
            string nome = d.estado == Defeito.Estado.Intacto ? "???" : d.nome;
            linhas += marca + " " + nome + "\n";
        }
        linhas += "\n" + Reparados() + " de " + defeitos.Length + " reparados\n";
        linhas += "Tempo: " + Mathf.FloorToInt(tempo / 60f) + ":" + Mathf.FloorToInt(tempo % 60f).ToString("00");
        linhas += "   Erros: " + erros;
        textoChecklist.text = linhas;
    }
}
