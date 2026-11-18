using System.Collections.Generic;
using UnityEngine;

// Coloque este script em cada defeito (fissura, mancha, lâmpada).
// O objeto precisa de: um Box Collider normal (para o raio selecionar),
// um segundo Box Collider com "Is Trigger" marcado e um pouco maior (para a ferramenta encostar)
// e o componente XR Simple Interactable.
public class Defeito : MonoBehaviour
{
    public enum Estado { Intacto, Identificado, Reparado }

    [Header("Informações mostradas no painel")]
    public string nome = "Fissura na parede";
    [TextArea(3, 6)]
    public string explicacao = "Causa: movimentação da estrutura e variação de temperatura.\nPrevenção: inspeções periódicas e reparo logo no início.";

    [Header("Reparo")]
    [Tooltip("Mesmo texto do campo 'tipo' do script Ferramenta (ex.: Espatula, Rolo). Deixe vazio na lâmpada, que é consertada por encaixe.")]
    public string ferramentaCorreta = "Espatula";
    [Tooltip("Desmarque se o raio não estiver conseguindo selecionar o defeito: aí a ferramenta certa conserta direto.")]
    public bool exigirIdentificacao = true;

    [Header("O que muda na cena")]
    public GameObject[] ativarAoIdentificar;
    public GameObject[] desativarAoIdentificar;
    public GameObject[] ativarAoReparar;
    public GameObject[] desativarAoReparar;

    [Header("Sons (opcional)")]
    public AudioClip somIdentificar;
    public AudioClip somReparar;
    public AudioClip somErro;

    public Estado estado { get; private set; } = Estado.Intacto;

    // Lista de todos os defeitos ativos na cena (usada pelo GerenciadorJogo).
    // Evita FindObjectsByType, cuja assinatura mudou entre versões do Unity 6.
    public static readonly List<Defeito> Todos = new List<Defeito>();

    void OnEnable() { if (!Todos.Contains(this)) Todos.Add(this); }
    void OnDisable() { Todos.Remove(this); }

    float ultimoErro = -10f;

    // Ligue em: XR Simple Interactable > Interactable Events > Select Entered
    public void Identificar()
    {
        if (estado != Estado.Intacto) return;

        estado = Estado.Identificado;
        Alternar(ativarAoIdentificar, true);
        Alternar(desativarAoIdentificar, false);
        Tocar(somIdentificar);

        if (GerenciadorJogo.Instancia != null)
            GerenciadorJogo.Instancia.DefeitoIdentificado(this);
    }

    void OnTriggerEnter(Collider outro)
    {
        if (estado == Estado.Reparado || string.IsNullOrEmpty(ferramentaCorreta)) return;

        Ferramenta ferramenta = outro.GetComponentInParent<Ferramenta>();
        if (ferramenta == null) return;

        if (estado == Estado.Intacto && !exigirIdentificacao && ferramenta.tipo == ferramentaCorreta)
        {
            Identificar();
        }
        else if (estado == Estado.Intacto)
        {
            if (GerenciadorJogo.Instancia != null)
                GerenciadorJogo.Instancia.Aviso("Primeiro registre o defeito: aponte o raio para ele e aperte o GRIP (botão lateral).");
            return;
        }

        if (ferramenta.tipo == ferramentaCorreta)
        {
            Reparar();
        }
        else if (Time.time - ultimoErro > 1.5f)
        {
            ultimoErro = Time.time;
            Tocar(somErro);
            if (GerenciadorJogo.Instancia != null)
                GerenciadorJogo.Instancia.RegistrarErro(ferramenta.tipo + " não serve para " + nome + ".");
        }
    }

    // A ferramenta certa chama isto sozinha.
    // Na lâmpada, ligue em: XR Socket Interactor > Interactor Events > Select Entered
    public void Reparar()
    {
        if (estado == Estado.Reparado) return;

        estado = Estado.Reparado;
        Alternar(ativarAoReparar, true);
        Alternar(desativarAoReparar, false);
        Tocar(somReparar);

        if (GerenciadorJogo.Instancia != null)
            GerenciadorJogo.Instancia.DefeitoReparado(this);
    }

    void Alternar(GameObject[] objetos, bool ativo)
    {
        if (objetos == null) return;
        foreach (GameObject obj in objetos)
            if (obj != null) obj.SetActive(ativo);
    }

    void Tocar(AudioClip som)
    {
        // Toca o som na posição do defeito (áudio espacial 3D)
        if (som != null) AudioSource.PlayClipAtPoint(som, transform.position);
    }
}
