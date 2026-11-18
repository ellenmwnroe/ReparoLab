using UnityEngine;
using UnityEngine.UIElements;

public class TestUI : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        VisualElement container = GetComponent<UIDocument>().rootVisualElement;

        Button botao1 = container.Q<Button>();

        botao1.clicked += () => mostrarMensagem();
    }

    private void mostrarMensagem(){
        Debug.Log("Botão 1 clicado");
    }

}
