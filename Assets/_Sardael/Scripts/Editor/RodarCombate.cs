using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Sardael;

namespace SardaelEditor
{
    /// <summary>
    /// Liga o teste automatico do duelo e entra em Play. Mesma disciplina do movimento:
    /// a cena fica DESLIGADA no disco, a bandeira de sessao liga so' nesta viagem.
    /// </summary>
    public static class RodarCombate
    {
        // Era a Combate_Duelo, que o MontarCombate criava. Essa cena nao existe mais — e o
        // construtor que a fazia esta quebrado (aponta pras pastas de pacote antigas). O duelo
        // agora mora na Movimento_Teste, montado pelo MontarDueloNaCena.
        const string CENA = "Assets/_Sardael/Cenas/Movimento_Teste.unity";

        [MenuItem("Sardael/Rodar Teste de Combate")]
        public static void Rodar()
        {
            if (EditorApplication.isPlaying) { EditorApplication.isPlaying = false; return; }

            var cena = EditorSceneManager.OpenScene(CENA, OpenSceneMode.Single);

            var duelo = Object.FindAnyObjectByType<Duelo>();
            if (duelo == null) { Debug.LogError("[Combate] nao achei o Duelo na cena"); return; }

            var teste = duelo.GetComponent<TesteDeCombate>();
            if (teste == null) teste = duelo.gameObject.AddComponent<TesteDeCombate>();
            teste.duelo = duelo;
            teste.heroi = duelo.GetComponent<MovimentoDoHeroi>();
            teste.olho = Object.FindAnyObjectByType<Camera>();
            teste.rodarAoIniciar = false;
            teste.sairAoTerminar = false;

            EditorUtility.SetDirty(teste);
            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);

            SessionState.SetBool("sardael.combate.auto", true);
            EditorApplication.EnterPlaymode();
        }
    }
}
