using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Interactables; // XR Interaction Toolkit 3.x

// Painel de diálogo estilo visual novel (world-space) que acompanha o jogador sem grudar no rosto.
// O botão "Continuar" é um BoxCollider + XR Simple Interactable: aponte o raio e aperte o GRIP (G no simulador).
public class DialogoVN : MonoBehaviour
{
    [Header("Textos")]
    public TMP_Text textoNome;
    public TMP_Text textoFala;

    [Header("Botão")]
    public GameObject botao;
    public Image fundoBotao;
    public TMP_Text textoBotao;
    public Color corBotao = new Color(1f, 0.55f, 0.12f);
    public Color corBotaoHover = new Color(1f, 0.75f, 0.35f);

    [Header("Acompanhar o jogador")]
    public Transform cabeca;
    public float distancia = 1.4f;
    public float altura = -0.3f;
    public float lateral = -0.3f;
    public float velocidade = 3f;
    public float anguloParaMover = 40f;

    [Header("Digitação")]
    public float letrasPorSegundo = 45f;

    float visiveis;
    int total;
    bool pedidoContinuar;
    bool seguindo;

    public bool Digitando => textoFala != null && textoFala.maxVisibleCharacters < total;
    public bool BotaoVisivel => botao != null && botao.activeSelf;

    void Awake()
    {
        if (botao != null)
        {
            var interagivel = botao.GetComponent<XRSimpleInteractable>();
            if (interagivel != null)
            {
                interagivel.selectEntered.AddListener(_ => Pressionar());
                interagivel.hoverEntered.AddListener(_ => { if (fundoBotao) fundoBotao.color = corBotaoHover; });
                interagivel.hoverExited.AddListener(_ => { if (fundoBotao) fundoBotao.color = corBotao; });
            }
        }
    }

    void Start()
    {
        if (cabeca == null && Camera.main != null) cabeca = Camera.main.transform;
        Posicionar(true);
    }

    // Mostra uma fala com efeito de digitação. textoDoBotao vazio = sem botão.
    public void Falar(string texto, string textoDoBotao = "")
    {
        if (textoFala == null) return;
        textoFala.text = texto;
        textoFala.ForceMeshUpdate();
        total = textoFala.textInfo.characterCount;
        visiveis = 0;
        textoFala.maxVisibleCharacters = 0;
        pedidoContinuar = false;
        MostrarBotao(textoDoBotao);
    }

    public void MostrarBotao(string texto)
    {
        if (botao == null) return;
        bool mostrar = !string.IsNullOrEmpty(texto);
        botao.SetActive(mostrar);
        if (mostrar && textoBotao != null) textoBotao.text = texto;
        if (fundoBotao) fundoBotao.color = corBotao;
    }

    // Retorna true uma única vez depois que o jogador aperta o botão (com o texto já completo)
    public bool ConsumirContinuar()
    {
        if (!pedidoContinuar) return false;
        pedidoContinuar = false;
        return true;
    }

    public void Pressionar()
    {
        if (Digitando) { visiveis = total; textoFala.maxVisibleCharacters = total; return; } // 1º clique completa o texto
        pedidoContinuar = true;
    }

    void Update()
    {
        if (textoFala != null && textoFala.maxVisibleCharacters < total)
        {
            visiveis += Time.deltaTime * letrasPorSegundo;
            textoFala.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(visiveis));
        }
    }

    void LateUpdate() => Posicionar(false);

    void Posicionar(bool imediato)
    {
        if (cabeca == null) return;
        Vector3 frente = Vector3.ProjectOnPlane(cabeca.forward, Vector3.up);
        if (frente.sqrMagnitude < 0.001f) return;
        frente.Normalize();
        Vector3 direita = Vector3.Cross(Vector3.up, frente);
        Vector3 alvo = cabeca.position + frente * distancia + direita * lateral + Vector3.up * altura;

        Vector3 paraPainel = Vector3.ProjectOnPlane(transform.position - cabeca.position, Vector3.up);
        bool longe = Vector3.Distance(transform.position, alvo) > 0.6f || Vector3.Angle(paraPainel, frente) > anguloParaMover;
        if (imediato) { transform.position = alvo; seguindo = false; }
        else
        {
            if (longe) seguindo = true;
            if (seguindo)
            {
                transform.position = Vector3.Lerp(transform.position, alvo, Time.deltaTime * velocidade);
                if (Vector3.Distance(transform.position, alvo) < 0.03f) seguindo = false;
            }
        }

        // O canvas olha para longe do jogador (assim o texto fica legível)
        Vector3 olhar = Vector3.ProjectOnPlane(transform.position - cabeca.position, Vector3.up);
        if (olhar.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(olhar, Vector3.up);
    }
}
