#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

// Menu: Reparo Lab > Adicionar trilha sonora
// Música de fundo 2D, em loop, com volume baixo para não brigar com a Lia e os efeitos.
public static class TrilhaReparoLab
{
    const string CaminhoClip = "Assets/ReparoLab/Audio/trilha_loop.wav"; // versão sem o silêncio final (gerada a partir de Assets/trilha.mp3)
    const float Volume = 0.18f; // a faixa é masterizada alta (média -12 dB): 0,18 deixa ela de fundo

    [MenuItem("Reparo Lab/Adicionar trilha sonora")]
    public static void Adicionar()
    {
        // Importação leve para o Quest 2: streaming (não ocupa RAM inteira) + Vorbis
        var importador = AssetImporter.GetAtPath(CaminhoClip) as AudioImporter;
        if (importador == null) { EditorUtility.DisplayDialog("Reparo Lab", "Não achei " + CaminhoClip, "OK"); return; }
        var cfg = importador.defaultSampleSettings;
        cfg.loadType = AudioClipLoadType.Streaming;
        cfg.compressionFormat = AudioCompressionFormat.Vorbis;
        cfg.quality = 0.6f;
        importador.defaultSampleSettings = cfg;
        importador.loadInBackground = true;
        importador.SaveAndReimport();

        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(CaminhoClip);
        Transform raiz = GameObject.Find("ReparoLab")?.transform;

        GameObject obj = raiz != null ? raiz.Find("Trilha Sonora")?.gameObject : GameObject.Find("Trilha Sonora");
        if (obj == null)
        {
            obj = new GameObject("Trilha Sonora");
            Undo.RegisterCreatedObjectUndo(obj, "Trilha");
            if (raiz != null) obj.transform.SetParent(raiz, false);
        }
        var fonte = obj.GetComponent<AudioSource>();
        if (fonte == null) fonte = Undo.AddComponent<AudioSource>(obj);
        Undo.RecordObject(fonte, "Trilha");
        fonte.clip = clip;
        fonte.loop = true;
        fonte.playOnAwake = true;
        fonte.volume = Volume;
        fonte.spatialBlend = 0f;        // 2D: mesmo volume em qualquer lugar do salão
        fonte.priority = 0;             // música nunca é cortada por falta de canais
        fonte.bypassReverbZones = true;
        fonte.dopplerLevel = 0f;

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Selection.activeGameObject = obj;
        Debug.Log("==== REPARO LAB: trilha sonora adicionada (loop, volume " + Volume + ") e cena salva ====");
    }
}
#endif
