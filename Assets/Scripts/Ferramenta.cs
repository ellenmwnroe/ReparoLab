using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables; // XR Interaction Toolkit 3.x

// Coloque este script em cada ferramenta (espátula, rolo de tinta, lâmpada nova).
// A ferramenta também precisa de XR Grab Interactable (que já adiciona o Rigidbody) e um Collider.
// Sem gravidade: se o jogador soltar, ela fica parada no ar (dá para pegar de novo ali mesmo)
// e depois de alguns segundos volta sozinha para a mesa. Nunca cai no "limbo".
public class Ferramenta : MonoBehaviour
{
    [Tooltip("Nome da ferramenta. Precisa ser igual ao campo 'Ferramenta Correta' do defeito. Ex.: Espatula, Rolo")]
    public string tipo = "Espatula";

    [Tooltip("Segundos parada no ar, depois de solta, antes de voltar para a mesa")]
    public float esperaAoSoltar = 3f;

    Vector3 posicaoInicial;
    Quaternion rotacaoInicial;
    Rigidbody corpo;
    XRGrabInteractable pegavel;
    float tempoSolta;

    void Start()
    {
        posicaoInicial = transform.position;
        rotacaoInicial = transform.rotation;
        corpo = GetComponent<Rigidbody>();
        pegavel = GetComponent<XRGrabInteractable>();

        // Cinemático e sem gravidade: o XR Grab Interactable devolve esse estado ao soltar
        if (corpo != null)
        {
            corpo.useGravity = false;
            corpo.isKinematic = true;
        }
    }

    void Update()
    {
        bool segurando = pegavel != null && pegavel.isSelected; // inclui a lâmpada encaixada no soquete
        bool foraDoLugar = Vector3.Distance(transform.position, posicaoInicial) > 0.05f;

        if (!segurando && foraDoLugar)
        {
            tempoSolta += Time.deltaTime;
            if (tempoSolta >= esperaAoSoltar) Voltar();
        }
        else
        {
            tempoSolta = 0f;
        }
    }

    public void Voltar()
    {
        tempoSolta = 0f;
        if (corpo != null && !corpo.isKinematic)
        {
            corpo.linearVelocity = Vector3.zero;
            corpo.angularVelocity = Vector3.zero;
        }
        transform.SetPositionAndRotation(posicaoInicial, rotacaoInicial);
    }
}
