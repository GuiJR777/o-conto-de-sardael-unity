using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Sardael;

namespace SardaelEditor
{
    /// <summary>
    /// Poe o duelo de pe' dentro da Movimento_Teste: o heroi no controller de combate, um orc
    /// na regua, e o <see cref="Duelo"/> ligando os dois.
    ///
    /// POR QUE NAO E' O MontarCombate: aquele construtor monta os controllers do zero a partir
    /// dos FBX, e os caminhos dele apontam pra "Assets/SpearCombatAnimationV2" e
    /// "Assets/Kevin Iglesias" — pastas que hoje moram dentro de _Pacotes. Ele quebra na
    /// primeira linha e ainda montava uma cena (Combate_Duelo) que nao existe mais. Os
    /// controllers que ele gerou, esses sim, sobreviveram em Animacoes/Controladores. Entao
    /// aqui eu nao reconstruo nada: uso o que ja' esta' pronto e aprovado.
    ///
    /// Roda quantas vezes quiser — se o orc ja' estiver na cena, ele e' reaproveitado, nao
    /// duplicado. A POSICAO do orc so' e' escolhida por mim na primeira vez; depois disso ela
    /// e' sua e eu nao encosto.
    /// </summary>
    public static class MontarDueloNaCena
    {
        const string CENA       = "Assets/_Sardael/Cenas/Movimento_Teste.unity";
        const string ORC        = "Assets/_Sardael/Personagens/Orc.prefab";
        const string CTRL_HEROI = "Assets/_Sardael/Animacoes/Controladores/Sardael_Combate.controller";
        const string CTRL_ORC   = "Assets/_Sardael/Animacoes/Controladores/Orc_Combate.controller";

        /// <summary>Marca 5 da regua da cena. Longe o bastante pra ele andar ate' o golpe.</summary>
        const float MARCA_DO_ORC = 5f;

        [MenuItem("Sardael/Montar Duelo (Movimento_Teste)")]
        public static void Executar()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[Duelo] pare o Play primeiro."); return; }

            var cena = EditorSceneManager.OpenScene(CENA, OpenSceneMode.Single);

            var heroi = Object.FindAnyObjectByType<MovimentoDoHeroi>();
            if (heroi == null) { Debug.LogError("[Duelo] nao achei o Sardael em " + CENA); return; }

            // ---------------------------------------------------------- 1. o heroi
            // O Sardael_Combate NAO e' um controller separado do movimento: ele foi construido
            // POR CIMA do de movimento (Locomocao, pulo, esquiva, rolar, arranco continuam la')
            // e so' acrescenta Golpe1/2/3 e Atingido. Trocar aqui nao tira nada do que ja' funciona.
            var animHeroi = heroi.GetComponent<Animator>();
            var ctrlHeroi = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CTRL_HEROI);
            if (ctrlHeroi == null) { Debug.LogError("[Duelo] nao achei " + CTRL_HEROI); return; }
            animHeroi.runtimeAnimatorController = ctrlHeroi;

            // ---------------------------------------------------------- 2. o orc
            var orc = GameObject.Find("Orc");
            bool nasceuAgora = orc == null;
            if (nasceuAgora)
            {
                var molde = AssetDatabase.LoadAssetAtPath<GameObject>(ORC);
                if (molde == null) { Debug.LogError("[Duelo] nao achei " + ORC); return; }
                orc = (GameObject)PrefabUtility.InstantiatePrefab(molde);
                orc.name = "Orc";
                orc.transform.position = new Vector3(heroi.transform.position.x + MARCA_DO_ORC,
                                                     0f, heroi.transform.position.z);
            }

            // encarando o heroi. Sao os dois unicos angulos que o Duelo usa (ver Encarar()):
            // orc a direita do heroi = -90, a esquerda = +90. Em Play o Duelo mantem isso.
            orc.transform.rotation = Quaternion.Euler(
                0f, orc.transform.position.x >= heroi.transform.position.x ? -90f : 90f, 0f);

            var animOrc = orc.GetComponent<Animator>();
            if (animOrc == null) animOrc = orc.AddComponent<Animator>();
            var ctrlOrc = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CTRL_ORC);
            if (ctrlOrc == null) { Debug.LogError("[Duelo] nao achei " + CTRL_ORC); return; }
            animOrc.runtimeAnimatorController = ctrlOrc;

            // quem liga o root motion e' o CorpoDoOrc, no Awake dele, e ele so' deixa a reacao
            // pareada deslocar. Sem este componente o clipe PARADO empurra o orc embora sozinho
            if (orc.GetComponent<CorpoDoOrc>() == null) orc.AddComponent<CorpoDoOrc>();

            // ---------------------------------------------------------- 3. o duelo
            // mora no Sardael: ele le' o MovimentoDoHeroi do proprio objeto pra saber pra que
            // lado o heroi olha
            var duelo = heroi.GetComponent<Duelo>();
            if (duelo == null) duelo = heroi.gameObject.AddComponent<Duelo>();
            duelo.doHeroi = animHeroi;
            duelo.doOrc = animOrc;
            duelo.orcAtacaSozinho = true;

            EditorUtility.SetDirty(heroi.gameObject);
            EditorUtility.SetDirty(orc);
            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);

            float vao = orc.transform.position.x - heroi.transform.position.x;
            Debug.Log(string.Format(
                "[Duelo] montado em {0}.\n"
              + "  heroi em x={1:F2} com o controller de combate\n"
              + "  orc {2} em x={3:F2}  (vao de {4:F2} m; o golpe alcanca {5:F2} ± {6:F2})\n"
              + "  J ataca. Agora rode: Sardael > Rodar Teste de Combate",
                CENA, heroi.transform.position.x, nasceuAgora ? "novo" : "que ja' estava la'",
                orc.transform.position.x, vao, Duelo.ALCANCE, duelo.folga));
        }
    }
}
