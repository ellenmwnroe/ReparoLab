#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

// Menu: Reparo Lab > Adicionar sons 3D
// Áudio espacial simples (requisito do Setup Técnico): os sons saem da posição de cada defeito.
//  - registrar / consertar / ferramenta errada: Defeito.Tocar usa AudioSource.PlayClipAtPoint (3D)
//  - zumbido em loop na lâmpada queimada (AudioSource 3D que some quando a lâmpada é trocada)
//  - vitória ao liberar o espaço
// Sons sintetizados para o projeto (sem arquivos de terceiros).
public static class SonsReparoLab
{
    const string Pasta = "Assets/ReparoLab/Audio/Sfx/";

    [MenuItem("Reparo Lab/Adicionar sons 3D")]
    public static void Adicionar()
    {
        AudioClip registrar = Clip("sfx_registrar.wav");
        AudioClip reparar = Clip("sfx_reparar.wav");
        AudioClip erro = Clip("sfx_erro.wav");
        AudioClip vitoria = Clip("sfx_vitoria.wav");
        AudioClip zumbido = Clip("sfx_zumbido_loop.wav");

        Transform raiz = GameObject.Find("ReparoLab")?.transform;
        if (raiz == null) { EditorUtility.DisplayDialog("Reparo Lab", "Não achei o objeto ReparoLab na cena.", "OK"); return; }

        int n = 0;
        foreach (var d in raiz.GetComponentsInChildren<Defeito>(true))
        {
            Undo.RecordObject(d, "Sons");
            d.somIdentificar = registrar;
            d.somReparar = reparar;
            d.somErro = erro;
            EditorUtility.SetDirty(d);
            n++;
        }

        // Zumbido 3D na lâmpada queimada: fica no visual "Lampada Apagada", que é desativado no conserto
        Transform apagada = raiz.Find("Lampada Queimada/Lampada Apagada");
        if (apagada != null)
        {
            var fonte = apagada.GetComponent<AudioSource>();
            if (fonte == null) fonte = Undo.AddComponent<AudioSource>(apagada.gameObject);
            Undo.RecordObject(fonte, "Zumbido");
            fonte.clip = zumbido;
            fonte.loop = true;
            fonte.playOnAwake = true;
            fonte.volume = 0.6f;
            fonte.spatialBlend = 1f;                       // 100% 3D
            fonte.rolloffMode = AudioRolloffMode.Logarithmic;
            fonte.minDistance = 1.5f;
            fonte.maxDistance = 10f;
            fonte.dopplerLevel = 0f;
        }

        var ger = raiz.GetComponentInChildren<GerenciadorJogo>(true);
        if (ger != null)
        {
            Undo.RecordObject(ger, "Vitória");
            ger.somVitoria = vitoria;
            EditorUtility.SetDirty(ger);
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("==== REPARO LAB: sons 3D em " + n + " defeitos + zumbido da lâmpada + vitória. Cena salva ====");
    }

    static AudioClip Clip(string arquivo)
    {
        string caminho = Pasta + arquivo;
        var imp = AssetImporter.GetAtPath(caminho) as AudioImporter;
        if (imp != null)
        {
            // Sons curtos: descompacta ao carregar (sem custo de CPU ao tocar), ADPCM leve, mono
            var cfg = imp.defaultSampleSettings;
            cfg.loadType = AudioClipLoadType.DecompressOnLoad;
            cfg.compressionFormat = AudioCompressionFormat.ADPCM;
            imp.defaultSampleSettings = cfg;
            imp.forceToMono = true;
            imp.SaveAndReimport();
        }
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(caminho);
        if (clip == null) Debug.LogWarning("Reparo Lab: som não encontrado: " + caminho);
        return clip;
    }
}
#endif
