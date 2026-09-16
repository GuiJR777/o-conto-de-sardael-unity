using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;

namespace SardaelEditor
{
    /// <summary>
    /// Mede o TELEGRAFO de cada ataque que o orc poderia usar.
    ///
    /// O telegrafo e' o tempo entre o golpe comecar e o golpe chegar. E' o numero que decide se
    /// uma defesa e' decisao ou enfeite: o orc de hoje avisa por 0,354 s, e reacao humana a
    /// estimulo visual fica perto de 0,25 s — sobram 0,1 s pra pensar. O Ryse, de onde veio a
    /// ideia do aparo, da' entre 0,6 e 0,9 s.
    ///
    /// Antes de esticar qualquer clipe no tempo, vale olhar o que o pacote ja' tem: sao dez
    /// ataques de uma mao e quatro de duas, e nada garante que o primeiro da lista — que e' o que
    /// esta' em uso — seja o de braco mais longo.
    ///
    /// COMO MEDE: o mesmo metodo que o MontarCombate ja' usava e que produziu o 0,3222 gravado no
    /// AvisoNoImpacto — amostra o clipe em 90 passos e acha o instante em que a MAO DIREITA esta'
    /// mais a frente, no espaco do proprio corpo. Nao e' palpite nem leitura de olho: e' o pico
    /// do alcance, que e' onde a arma chega.
    ///
    /// Nao toca em nada. Instancia o orc, mede, e destroi a instancia no finally — posar rig no
    /// editor e deixar pra tras grava a pose inteira na cena.
    /// </summary>
    public static class MedirGolpesDoOrc
    {
        const string ORC  = "Assets/_Sardael/Personagens/Orc.prefab";
        const string KEV  = "Assets/_Pacotes/Kevin Iglesias/Human Animations/Animations/Male/Combat/";
        const string EM_USO = "Attack1H01_R";
        const string SAIDA = "_telegrafo_do_orc.txt";

        /// <summary>O alvo de desenho: quanto tempo o jogador precisa pra ver e decidir.</summary>
        const float ALVO = 0.70f;

        static readonly string[] CANDIDATOS = {
            "1H/HumanM@Attack1H01_L", "1H/HumanM@Attack1H01_R",
            "1H/HumanM@Attack1H02_L", "1H/HumanM@Attack1H02_R",
            "1H/HumanM@Attack1H03_L", "1H/HumanM@Attack1H03_R",
            "1H/HumanM@Attack1H04_L", "1H/HumanM@Attack1H04_R",
            "1H/HumanM@Attack1H05_L", "1H/HumanM@Attack1H05_R",
            "1H/HumanM@AttackDW01",   "1H/HumanM@AttackDW02",
            "2H/HumanM@Attack2H01",   "2H/HumanM@Attack2H02",
            "2H/HumanM@Attack2H03",   "2H/HumanM@Attack2H04",
        };

        class Medida
        {
            public string nome, caminho, mao;
            public float duracao, fracao, telegrafo, alcance, recuperacao;
            // o que o JOGADOR consegue ver antes do golpe chegar
            public float armarEm;      // fracao em que o braco chega no ponto morto da armada
            public float recuo;        // quanto a mao vai PRA TRAS na armada, em metros
            public float subida;       // quanto a mao SOBE na armada, em metros
            public float px;           // a armada convertida em pixels nesta camera
        }

        /// <summary>
        /// Pixels por metro no plano de jogo, a 1080p, com a camera DESTA cena.
        ///
        /// Sai da conta: FOV 42 vertical, camera a 7 m com 6 graus de inclinacao, linha de jogo
        /// em z=0, entao a faixa visivel na altura do tronco tem 5,42 m e 1080/5,42 = 199.
        ///
        /// Serve pra uma coisa so: transformar "a mao recuou 5 cm" em "sao 11 pixels", que e a
        /// pergunta de verdade. Movimento medido em metros engana; medido em pixels, nao.
        /// </summary>
        const float PX_POR_METRO = 199f;

        [MenuItem("Sardael/Medir Telegrafo dos Golpes do Orc")]
        public static void Executar()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[Telegrafo] pare o Play primeiro."); return; }

            var molde = AssetDatabase.LoadAssetAtPath<GameObject>(ORC);
            if (molde == null) { Debug.LogError("[Telegrafo] nao achei " + ORC); return; }

            var medidas = new List<Medida>();
            var corpo = (GameObject)PrefabUtility.InstantiatePrefab(molde);
            try
            {
                var anim = corpo.GetComponent<Animator>();
                // AS DUAS MAOS, e nao so' a direita.
                //
                // A lista tem ataques _L (mao esquerda) e AttackDW (duas armas). Medindo so' a
                // direita, nesses o pico que eu acharia seria o braco de tras se mexendo, e nao
                // a arma chegando — seis das dezesseis linhas sairiam erradas e parecendo certas.
                // Entao eu meco as duas e fico com a que vai mais longe, dizendo qual foi.
                var direita  = anim == null ? null : anim.GetBoneTransform(HumanBodyBones.RightHand);
                var esquerda = anim == null ? null : anim.GetBoneTransform(HumanBodyBones.LeftHand);
                if (direita == null && esquerda == null)
                { Debug.LogError("[Telegrafo] o orc nao tem mao nenhuma mapeada."); return; }

                AnimationMode.StartAnimationMode();
                try
                {
                    for (int c = 0; c < CANDIDATOS.Length; c++)
                    {
                        string caminho = KEV + CANDIDATOS[c] + ".fbx";
                        var clipe = Clipe(caminho);
                        if (clipe == null) { Debug.LogWarning("[Telegrafo] nao achei " + caminho); continue; }

                        EditorUtility.DisplayProgressBar("Medindo o telegrafo do orc",
                            clipe.name, (float)c / CANDIDATOS.Length);

                        var m = Medir(corpo, direita, esquerda, clipe);
                        m.caminho = caminho;
                        medidas.Add(m);
                    }
                }
                finally { AnimationMode.StopAnimationMode(); EditorUtility.ClearProgressBar(); }
            }
            finally { Object.DestroyImmediate(corpo); }

            Relatar(medidas);
        }

        static Medida Medir(GameObject corpo, Transform direita, Transform esquerda, AnimationClip clipe)
        {
            const int PASSOS = 90;                // o mesmo 90 que produziu o 0,3222 em uso hoje
            var z = new float[PASSOS + 1];        // quanto a mao esta a frente, no corpo do orc
            var y = new float[PASSOS + 1];        // e quanto ela esta alta

            // Guardo as amostras e analiso depois. Antes eu so guardava o MAXIMO, e maximo
            // sozinho responde "onde a arma chega" — nao responde "da pra VER que ela vem",
            // que e a pergunta que decide se alongar o telegrafo adianta alguma coisa.
            AnimationMode.BeginSampling();
            for (int i = 0; i <= PASSOS; i++)
            {
                AnimationMode.SampleAnimationClip(corpo, clipe, clipe.length * (float)i / PASSOS);
                z[i] = float.NegativeInfinity;
                if (direita != null)
                {
                    var p = corpo.transform.InverseTransformPoint(direita.position);
                    z[i] = p.z; y[i] = p.y;
                }
                if (esquerda != null)
                {
                    var p = corpo.transform.InverseTransformPoint(esquerda.position);
                    if (p.z > z[i]) { z[i] = p.z; y[i] = p.y; }
                }
            }
            AnimationMode.EndSampling();

            // 1) O IMPACTO: onde a mao chega mais longe. E o 0,3222 que ja esta em uso.
            int pico = 0;
            for (int i = 1; i <= PASSOS; i++) if (z[i] > z[pico]) pico = i;

            // qual mao fez o pico — refaz so no quadro do pico, e barato
            string qualMao;
            if (direita != null && esquerda != null)
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(corpo, clipe, clipe.length * (float)pico / PASSOS);
                float zd = corpo.transform.InverseTransformPoint(direita.position).z;
                float ze = corpo.transform.InverseTransformPoint(esquerda.position).z;
                AnimationMode.EndSampling();
                qualMao = zd >= ze ? "dir" : "esq";
            }
            else qualMao = direita != null ? "dir" : "esq";

            // 2) O PONTO MORTO DA ARMADA: antes do impacto, onde a mao esta mais PRA TRAS. E o
            //    alto do movimento, onde o braco para antes de descer. Ele tem dois usos:
            //    e o que o jogador ve como "vem golpe", e e onde a velocidade pode voltar a 1,0
            //    sem engasgo — ali o movimento ja esta parado, entao a guinada e invisivel.
            int armar = 0;
            for (int i = 1; i <= pico; i++) if (z[i] < z[armar]) armar = i;

            // 3) QUANTO DISSO O OLHO PEGA: o quanto a mao anda entre a guarda e o ponto morto,
            //    pra tras e pra cima. E isso, em pixels, que decide se o aviso existe.
            float subiu = 0f;
            for (int i = armar; i <= pico; i++) if (y[i] - y[armar] > subiu) subiu = y[i] - y[armar];
            float recuou = z[0] - z[armar];

            float fracao = (float)pico / PASSOS;
            return new Medida {
                nome = clipe.name,
                duracao = clipe.length,
                fracao = fracao,
                telegrafo = fracao * clipe.length,
                recuperacao = (1f - fracao) * clipe.length,
                alcance = z[pico],
                mao = qualMao,
                armarEm = (float)armar / PASSOS,
                recuo = recuou,
                subida = subiu,
                px = Mathf.Max(Mathf.Abs(recuou), subiu) * PX_POR_METRO,
            };
        }

        static void Relatar(List<Medida> medidas)
        {
            if (medidas.Count == 0) { Debug.LogError("[Telegrafo] nao medi nada."); return; }
            medidas.Sort(delegate (Medida a, Medida b) { return b.telegrafo.CompareTo(a.telegrafo); });

            var sb = new StringBuilder();
            sb.AppendLine("===== TELEGRAFO DOS GOLPES DO ORC =====");
            sb.AppendLine("Unity " + Application.unityVersion + "   " + System.DateTime.Now);
            sb.AppendLine();
            sb.AppendLine("telegrafo = quanto tempo entre o golpe comecar e a arma chegar.");
            sb.AppendLine("Medido com a mao direita mais a frente, 90 passos por clipe — o mesmo");
            sb.AppendLine("metodo que gravou o 0,3222 do AvisoNoImpacto que esta' em uso.");
            sb.AppendLine();
            sb.AppendLine("Alvo de desenho: " + ALVO.ToString("F2") + " s  (reacao humana ~0,25 s + tempo de decidir)");
            sb.AppendLine();
            sb.AppendLine("  telegrafo  recuper.  pico  armar   recuo  subida    px  mao  nome");
            sb.AppendLine("  ---------  --------  ----  -----  ------  ------  ----  ---  ----");

            foreach (var m in medidas)
            {
                bool emUso = m.nome.Contains(EM_USO);
                sb.AppendLine(string.Format(
                    "  {0,7:F3} s  {1,6:F3} s  {2,3:P0}  {3,4:P0}  {4,6:F3}  {5,6:F3}  {6,4:F0}  {7,3}  {8}{9}",
                    m.telegrafo, m.recuperacao, m.fracao, m.armarEm, m.recuo, m.subida, m.px, m.mao,
                    m.nome, emUso ? "   <<< EM USO HOJE" : ""));
            }

            var melhor = medidas[0];
            sb.AppendLine();
            sb.AppendLine("O MAIS LONGO: " + melhor.nome + " com " + melhor.telegrafo.ToString("F3") + " s");
            if (melhor.telegrafo >= ALVO)
                sb.AppendLine("  Ja' alcanca o alvo sozinho. Trocar o clipe basta — nao precisa esticar nada.");
            else
                sb.AppendLine(string.Format(
                    "  Ainda fica {0:F3} s abaixo do alvo. Pra chegar la' o comeco do golpe teria que\n"
                  + "  rodar a {1:F2} da velocidade ate' o impacto, e voltar a 1,00 depois — braco que\n"
                  + "  sobe devagar e desce rapido. O fator sai da conta, nao do gosto.",
                    ALVO - melhor.telegrafo, melhor.telegrafo / ALVO));

            sb.AppendLine();
            sb.AppendLine("COMO LER AS COLUNAS");
            sb.AppendLine("  pico    fracao do clipe onde a arma chega. E esse numero que vai pro campo");
            sb.AppendLine("          'quando' do AvisoNoImpacto se o golpe for trocado.");
            sb.AppendLine("  armar   fracao do ponto morto da armada — o alto do movimento. E onde a");
            sb.AppendLine("          velocidade pode voltar a 1,0 sem engasgo, se for pra esticar so o comeco.");
            sb.AppendLine("  recuo   quanto a mao vai pra tras da guarda ate o ponto morto (metros).");
            sb.AppendLine("  subida  quanto ela sobe no mesmo trecho (metros).");
            sb.AppendLine("  px      o maior dos dois, em pixels nesta camera a 1080p. ESTA E A COLUNA");
            sb.AppendLine("          QUE DECIDE. O golpe em uso hoje da 11 px de armada: alongar o tempo");
            sb.AppendLine("          dele so daria ao jogador mais segundos olhando pra 11 pixels.");
            sb.AppendLine("          Abaixo de ~40 px o aviso nao existe, por mais lento que fique.");
            sb.AppendLine();
            sb.AppendLine("A COLUNA 'mao' diz qual mao foi mais longe. Nos _L e nos DW ela devia dar 'esq';");
            sb.AppendLine("se der 'dir' num deles, o pico achado provavelmente NAO e' a arma chegando —");
            sb.AppendLine("desconfie dessa linha.");
            sb.AppendLine();
            sb.AppendLine("E a coluna 'alcance' e' o quanto a mao chega a frente do corpo. Compare com o");
            sb.AppendLine("ALCANCE do duelo (1,756 m): um golpe com pico curto pode nem encostar no heroi.");

            string arq = Application.dataPath + "/../" + SAIDA;
            try { System.IO.File.WriteAllText(arq, sb.ToString()); } catch { }
            Debug.Log(sb.ToString() + "\ngravado em " + arq);
        }

        static AnimationClip Clipe(string caminho)
        {
            var tudo = AssetDatabase.LoadAllAssetsAtPath(caminho);
            if (tudo == null) return null;
            foreach (var a in tudo)
            {
                var c = a as AnimationClip;
                if (c != null && !c.name.StartsWith("__preview__")) return c;
            }
            return null;
        }
    }
}
