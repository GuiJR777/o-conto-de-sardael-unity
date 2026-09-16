using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Sardael;

namespace SardaelEditor
{
    /// <summary>
    /// Menu Sardael > Lancas.
    ///
    /// "Montar o acervo" varre a pasta das lancas e refaz o asset de Resources. Rode depois de
    /// jogar FBX novo na pasta ou de apagar algum — o acervo e' derivado, nunca editado a mao,
    /// entao nao tem como ele ficar dessincronizado do disco.
    ///
    /// "Apagar as marcadas" le o txt que o catalogo escreve em tempo de jogo e manda os FBX pra
    /// LIXEIRA, nao pro nada: dono de projeto muda de ideia, e desfazer um DeleteAsset nao existe.
    /// </summary>
    public static class FerramentasDeLanca
    {
        const string PASTA    = "Assets/_Sardael/Externos/Lancas";
        const string RETRATOS = "Assets/_Sardael/Resources/Lancas";
        const string ACERVO   = "Assets/_Sardael/Resources/Acervo_De_Lancas.asset";
        const string MARCADAS = PASTA + "/marcadas_pra_apagar.txt";

        [MenuItem("Sardael/Lancas/Montar o acervo")]
        public static void Montar()
        {
            var acervo = AssetDatabase.LoadAssetAtPath<AcervoDeLancas>(ACERVO);
            if (acervo == null)
            {
                acervo = ScriptableObject.CreateInstance<AcervoDeLancas>();
                Directory.CreateDirectory("Assets/_Sardael/Resources");
                AssetDatabase.CreateAsset(acervo, ACERVO);
            }

            var lista = new List<LancaDoAcervo>();
            foreach (var p in AssetDatabase.FindAssets("t:GameObject", new[] { PASTA })
                                           .Select(AssetDatabase.GUIDToAssetPath)
                                           .OrderBy(p => p))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (go == null) continue;
                var mf = go.GetComponentInChildren<MeshFilter>(true);
                if (mf == null || mf.sharedMesh == null) continue;

                // "Lanca_A07" -> nome "A07", pacote "A"
                string arquivo = Path.GetFileNameWithoutExtension(p);
                string nome = arquivo.StartsWith("Lanca_") ? arquivo.Substring(6) : arquivo;

                lista.Add(new LancaDoAcervo
                {
                    nome = nome,
                    pacote = nome.Length > 0 ? nome.Substring(0, 1) : "?",
                    modelo = go,
                    retrato = AssetDatabase.LoadAssetAtPath<Texture2D>(RETRATOS + "/Retrato_" + nome + ".png"),
                    alturaCrua = mf.sharedMesh.bounds.size.y * go.transform.localScale.y
                });
            }

            acervo.lancas = lista.ToArray();
            EditorUtility.SetDirty(acervo);
            AssetDatabase.SaveAssets();

            int semRetrato = lista.Count(l => l.retrato == null);
            Debug.Log("Acervo de lancas montado: " + lista.Count + " modelos"
                    + (semRetrato > 0 ? "  (" + semRetrato + " sem retrato)" : ""), acervo);
        }

        [MenuItem("Sardael/Lancas/Apagar as marcadas")]
        public static void ApagarMarcadas()
        {
            if (!File.Exists(MARCADAS))
            {
                EditorUtility.DisplayDialog("Lancas",
                    "Ninguem marcou nada ainda.\n\nAbra o catalogo em Movimento_Teste (tecla TAB) "
                  + "e clique com o botao direito nas lancas que voce quer fora.", "ta");
                return;
            }

            var nomes = File.ReadAllLines(MARCADAS)
                            .Select(l => l.Trim())
                            .Where(l => l.Length > 0)
                            .Distinct().ToList();
            if (nomes.Count == 0) { Debug.Log("Nenhuma lanca marcada."); return; }

            if (!EditorUtility.DisplayDialog("Apagar " + nomes.Count + " lancas?",
                    string.Join(", ", nomes.ToArray()) + "\n\nVao pra lixeira do Windows, "
                  + "da' pra recuperar de la'.", "apagar", "deixa quieto")) return;

            var sb = new StringBuilder();
            foreach (var n in nomes)
            {
                foreach (var sub in new[] { "/A/", "/B/" })
                {
                    string p = PASTA + sub + "Lanca_" + n + ".fbx";
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(p) != null)
                        sb.AppendLine((AssetDatabase.MoveAssetToTrash(p) ? "apagada: " : "FALHOU: ") + p);
                }
                string r = RETRATOS + "/Retrato_" + n + ".png";
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(r) != null) AssetDatabase.MoveAssetToTrash(r);
            }

            AssetDatabase.MoveAssetToTrash(MARCADAS);
            AssetDatabase.Refresh();
            Montar();                      // o acervo tem que refletir o disco na hora
            Debug.Log(sb.ToString());
        }
    }
}
