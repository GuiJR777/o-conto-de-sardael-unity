using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Sardael;
using Sardael.EditorFerramentas;

namespace SardaelEditor
{
    /// <summary>
    /// Mede A FORMA de cada um dos 35 golpes do pacote: onde a lamina chega, em que altura, e se
    /// o golpe termina com o Sardael encarando a linha do jogo.
    ///
    /// POR QUE ISTO EXISTE: a folha de contato respondeu "sao parecidos?" no olho, e uma conta de
    /// silhueta sobre as tiras respondeu que nenhum par e' duplicata. As duas param no mesmo
    /// lugar: nao dizem se o golpe ENCOSTA no orc. Numa mao de cartas isso e' o texto da carta.
    ///
    /// DUAS MEDIDAS MINHAS QUE DERAM ERRADO ANTES DESTA, E COMO
    /// --------------------------------------------------------
    /// 1) A PONTA ERA "O CANTO MAIS A FRENTE DA CAIXA DA LANCA".
    ///    A caixa tem oito cantos e, conforme a lanca gira, o canto vencedor TROCA. Cada troca e'
    ///    um salto que nao existe no movimento. O curso saiu em 70 m num clipe de 4,3 s — a ponta
    ///    teria feito sete voltas completas. Agora a ponta e' UM ponto fixo no modelo (o topo da
    ///    malha no +Y local, que e' como o EncaixeDeArma ja' a encontrava), e o caminho e' o
    ///    caminho.
    ///
    /// 2) O GIRO ERA O MAXIMO DO CLIPE INTEIRO.
    ///    Todo golpe de haste torce o tronco: arma para tras, solta para frente. O maximo dessa
    ///    torcao e' grande em QUALQUER ataque de verdade — por isso a primeira rodada reprovou 35
    ///    de 35 e nao separou nada. O que decide se o golpe le' de perfil nao e' o pico da torcao:
    ///    e' para onde ele esta' virado NO INSTANTE DO IMPACTO, e se ele volta para a linha no
    ///    fim. Sao dois instantes, nao um maximo.
    ///
    /// 3) O GIRO ERA CONTRA ZERO, E A GUARDA NAO ESTA' EM ZERO.
    ///    Medi o quanto o peito foge de "de frente para o orc" e chamei de defeito. Mas a guarda
    ///    de haste deste pacote ja' nasce com o peito a 50 graus da linha — postura de lanca e'
    ///    de lado, como esgrima. Contra zero, ate' parado o Sardael estaria errado. A referencia
    ///    e' a GUARDA, e ela sai impressa no cabecalho para a tabela poder ser lida.
    ///
    /// O QUE DECIDE SE A CARTA PRESTA: A ALTURA, CONTRA O CORPO MEDIDO DO ORC
    /// ---------------------------------------------------------------------
    /// O orc tem capsula de 1,699 m de altura e 0,191 m de raio (medida nos ossos dele, gravada
    /// em _corpo_do_orc.txt). Entao um golpe so' encosta se a ponta passar entre o chao e o alto
    /// da cabeca. Isso nao e' gosto meu: e' o colisor que ja' esta' no prefab.
    ///
    /// Golpe cuja ponta passa ACIMA disso nao errou de pouco — ele nao usa a lanca. O
    /// Attack1_Stage1, que hoje e' o elo 1, chega com a ponta a 2,75 m: a lanca esta' erguida e
    /// quem bate e' o PE. Por isso o pe tambem e' medido, e o veredito diz 'CHUTE' quando ele
    /// chega mais longe que a ponta.
    ///
    /// Nao salva a cena e devolve os ossos no finally.
    /// </summary>
    public static class MedirAFormaDosGolpes
    {
        const string CENA = "Assets/_Sardael/Cenas/Movimento_Teste.unity";
        const string RM   = "Assets/_Pacotes/SpearCombatAnimationV2/Animation/RM/A_SpearCombatAnimationV2_";
        const string SAIDA = "_forma_dos_golpes.txt";
        const string CSV   = "_forma_dos_golpes.csv";
        const int PASSOS = 90;

        /// <summary>Altura da capsula do orc, medida nos ossos dele (MontarCorpoDoOrc).</summary>
        const float ALTO_DO_ORC = 1.699f;
        /// <summary>Abaixo disto a ponta esta' raspando o chao, nao o corpo.</summary>
        const float PE_DO_ORC = 0.15f;
        /// <summary>Raio da capsula do orc: alem disso, de perfil, a ponta passa ao lado dele.</summary>
        const float RAIO_DO_ORC = 0.191f;

        /// <summary>A mesma tabela da folha de contato: contada nos arquivos, nao chutada.</summary>
        static readonly int[] ELOS_POR_ATAQUE = {
            0, 2, 2, 3, 3, 0, 3, 3, 4, 0, 2, 0, 0, 0,
        };

        class Forma
        {
            public string nome;
            public bool unico;
            public float duracao, quando;
            public float alcance, altura, desvio;    // da ponta da lanca, no instante do alcance
            public float teto, curso;
            public float pe, peAltura;               // o pe no instante em que ele vai mais longe
            public float giroNoImpacto, giroNoFim, guarda, torcao;
            public float avanco;

            /// <summary>
            /// Quem bate e' a perna: o pe avanca E sai do chao.
            ///
            /// So' comparar pe com alcance nao serve — no chute do elo 1 a lanca fica erguida e
            /// ainda assim projeta 1,20 m para a frente, mais que os 0,83 m do pe. O que separa
            /// um chute de um passo e' o pe estar NO AR.
            /// </summary>
            public bool Chuta { get { return peAltura > 0.35f && pe > 0.50f; } }
        }

        [MenuItem("Sardael/Medir a Forma dos 35 Golpes")]
        public static void Executar()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[Forma] pare o Play primeiro."); return; }

            var alvos = new List<string>();
            for (int a = 1; a < ELOS_POR_ATAQUE.Length; a++) alvos.Add("Attack" + a);
            for (int a = 1; a < ELOS_POR_ATAQUE.Length; a++)
                for (int s = 1; s <= ELOS_POR_ATAQUE[a]; s++)
                    alvos.Add("Attack" + a + "_Stage" + s + "_Complete");

            EditorSceneManager.OpenScene(CENA, OpenSceneMode.Single);

            var heroi = Object.FindAnyObjectByType<MovimentoDoHeroi>();
            if (heroi == null) { Debug.LogError("[Forma] nao achei o Sardael em " + CENA); return; }
            var anim = heroi.GetComponent<Animator>();
            if (anim == null) { Debug.LogError("[Forma] o Sardael nao tem Animator."); return; }

            var naMao = heroi.GetComponent<LancaNaMao>();
            Transform lanca = naMao != null ? naMao.lancaOriginal : null;
            if (lanca == null)
            { Debug.LogError("[Forma] nao achei a lanca (LancaNaMao.lancaOriginal)."); return; }

            // A PONTA: um ponto so', achado ANTES de posar.
            // O topo da malha no +Y local — a mesma convencao que o EncaixeDeArma usa para
            // conferir o alcance acima da mao. Guardo o MeshFilter e o ponto local; dai' em
            // diante a ponta e' sempre o mesmo ponto do modelo, gire a lanca como girar.
            MeshFilter malha = null; Vector3 pontaLocal = Vector3.zero;
            foreach (var mf in lanca.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var b = mf.sharedMesh.bounds;
                if (malha == null || b.size.y > malha.sharedMesh.bounds.size.y)
                { malha = mf; pontaLocal = new Vector3(b.center.x, b.max.y, b.center.z); }
            }
            if (malha == null) { Debug.LogError("[Forma] a lanca nao tem malha para achar a ponta."); return; }

            // os ossos ANTES de posar
            var ombroE = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var ombroD = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var peD    = anim.GetBoneTransform(HumanBodyBones.RightFoot);
            var peE    = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            if (ombroE == null || ombroD == null)
            { Debug.LogError("[Forma] faltam ombros no mapeamento humanoide do Sardael."); return; }

            var achadas = new List<Forma>();
            int faltando = 0;
            GravadorDeQuadros.Guardar(heroi.transform);
            try
            {
                for (int c = 0; c < alvos.Count; c++)
                {
                    var clipe = Clipe(RM + alvos[c] + "_RM.FBX");
                    if (clipe == null) { faltando++; continue; }
                    EditorUtility.DisplayProgressBar("Medindo a forma", clipe.name, (float)c / alvos.Count);

                    var f = Medir(heroi.transform, anim, malha, pontaLocal,
                                  ombroE, ombroD, peD, peE, clipe);
                    f.nome = alvos[c];
                    f.unico = !alvos[c].Contains("_Stage");
                    var v = clipe.averageSpeed;
                    f.avanco = new Vector2(v.x, v.z).magnitude * clipe.length;
                    achadas.Add(f);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                GravadorDeQuadros.Devolver();      // os ossos voltam. Sempre.
            }

            Relatar(achadas, faltando);
        }

        static Forma Medir(Transform heroi, Animator anim, MeshFilter malha, Vector3 pontaLocal,
                           Transform ombroE, Transform ombroD, Transform peD, Transform peE,
                           AnimationClip clipe)
        {
            var f = new Forma { duracao = clipe.length };
            float maisLonge = float.NegativeInfinity, teto = float.NegativeInfinity;
            float curso = 0f, torcao = 0f, melhorPe = float.NegativeInfinity;
            Vector3 anterior = Vector3.zero;
            int passoDoImpacto = 0;
            var giros = new float[PASSOS + 1];

            for (int i = 0; i <= PASSOS; i++)
            {
                // O ULTIMO PASSO NAO E' clipe.length.
                //
                // Estes clipes vem com loopTime: 1, e num clipe em laco o instante final E' o
                // instante zero. Amostrando em length eu media a GUARDA e chamava de "fim do
                // golpe" — foi por isso que giro@fim saiu exatamente 50 graus nas 35 linhas: era
                // sempre a mesma pose, a do quadro zero. Valor igual em 35 clipes diferentes nao
                // e' coincidencia, e' a medida olhando para o lugar errado.
                float t = Mathf.Min(clipe.length * i / PASSOS,
                                    Mathf.Max(0f, clipe.length - 1f / 30f));
                GravadorDeQuadros.Pousar(anim, clipe, t);

                // a ponta, no referencial do heroi: x = lado, y = altura, z = frente
                Vector3 p = heroi.InverseTransformPoint(
                    malha.transform.TransformPoint(pontaLocal));
                if (i > 0) curso += Vector3.Distance(p, anterior);
                anterior = p;

                if (p.z > maisLonge)
                {
                    maisLonge = p.z; f.altura = p.y; f.desvio = Mathf.Abs(p.x);
                    f.quando = (float)i / PASSOS; passoDoImpacto = i;
                }
                if (p.y > teto) teto = p.y;

                foreach (var pe in new[] { peD, peE })
                {
                    if (pe == null) continue;
                    var q = heroi.InverseTransformPoint(pe.position);
                    if (q.z > melhorPe) { melhorPe = q.z; f.peAltura = q.y; }
                }

                giros[i] = GiroDoTronco(heroi, ombroE, ombroD);
                torcao = Mathf.Max(torcao, Mathf.Abs(giros[i]));
            }

            f.alcance = maisLonge; f.teto = teto; f.curso = curso; f.pe = melhorPe;
            f.torcao = torcao;
            f.guarda = giros[0];                  // o quadro zero e' a guarda: a referencia
            f.giroNoImpacto = giros[passoDoImpacto];
            f.giroNoFim = giros[PASSOS];
            return f;
        }

        /// <summary>
        /// O quanto o tronco esta' virado para fora da linha, em graus, nesta pose.
        ///
        /// Pela linha dos ombros: num personagem virado para +Z local ela aponta para +X, e o
        /// quanto ela foge disso e' o quanto ele virou. A raiz nao serve para isto — com
        /// loopBlendPositionXZ ligado, que e' como os 13 chegam, a raiz nunca gira e a volta
        /// inteira fica assada na pose.
        /// </summary>
        static float GiroDoTronco(Transform heroi, Transform ombroE, Transform ombroD)
        {
            var e = heroi.InverseTransformPoint(ombroE.position);
            var d = heroi.InverseTransformPoint(ombroD.position);
            var s = new Vector3(d.x - e.x, 0f, d.z - e.z);
            if (s.sqrMagnitude < 1e-6f) return 0f;

            // COM SINAL, e pelo PEITO, nao pela linha dos ombros.
            //
            // A linha dos ombros e' uma reta, e reta nao tem frente: virado 170 graus ela mede
            // igual a virado 10. A primeira versao dobrava tudo para [0,90] e com isso dizia que
            // estar de COSTAS para o orc era a mesma coisa que estar de frente. O peito tem
            // frente, entao e' ele que responde.
            var peito = Vector3.Cross(s.normalized, Vector3.up);       // +X x +Y = +Z
            return Vector2.SignedAngle(new Vector2(0f, 1f),
                                       new Vector2(peito.x, peito.z).normalized);
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

        // ------------------------------------------------------------------ o relatorio

        /// <summary>
        /// O veredito da carta, contra o corpo medido do orc — nao contra gosto meu.
        /// </summary>
        static string Veredito(Forma f)
        {
            // A ALTURA PRIMEIRO, e sempre. Ela responde a pergunta de baixo — a ponta passa
            // pelo corpo do orc? — e nenhuma outra coluna substitui essa.
            if (f.altura > ALTO_DO_ORC)
                return f.Chuta ? "CHUTE (lanca por cima)" : "lanca passa por cima";
            if (f.altura < PE_DO_ORC) return "raspa o chao";
            if (f.desvio > RAIO_DO_ORC * 3f) return "passa ao lado";
            if (f.altura > 1.20f) return "acerta em cima";
            if (f.altura < 0.70f) return "acerta embaixo";
            return "acerta no tronco";
        }

        static void Relatar(List<Forma> m, int faltando)
        {
            var sb = new StringBuilder();
            sb.AppendLine("===== A FORMA DOS 35 GOLPES =====");
            sb.AppendLine("Unity " + Application.unityVersion + "   " + System.DateTime.Now);
            sb.AppendLine();
            sb.AppendLine("A PONTA e' um ponto fixo do modelo (topo da malha no +Y local), amostrado em "
                        + PASSOS + " passos");
            sb.AppendLine("por clipe no referencial do Sardael. O veredito e' contra a capsula MEDIDA do orc:");
            sb.AppendLine(string.Format("{0:F3} m de altura, {1:F3} m de raio.", ALTO_DO_ORC, RAIO_DO_ORC));
            sb.AppendLine();
            sb.AppendLine(string.Format(
                "A GUARDA (quadro zero) tem o peito a {0:F0} graus da linha. Leia a coluna giro contra ESSE",
                m.Count > 0 ? m[0].guarda : 0f));
            sb.AppendLine("numero, nao contra zero: postura de haste e' naturalmente de lado.");
            sb.AppendLine();
            sb.AppendLine("  clipe                      dur    alcance   pe     pe.alt  altura  desvio  teto   curso   giro@imp  giro@fim  balanco  veredito");
            sb.AppendLine("  -------------------------  -----  -------  ------  ------  ------  ------  -----  ------  --------  --------  -------  ---------------------");
            m.Sort((a, b) => Mathf.Abs(a.giroNoImpacto).CompareTo(Mathf.Abs(b.giroNoImpacto)));
            foreach (var x in m)
                sb.AppendLine(string.Format(
                    "  {0,-25}  {1,4:F2}s  {2,5:F2}m  {3,4:F2}m  {4,4:F2}m  {5,4:F2}m  {6,4:F2}m  {7,4:F2}m  {8,5:F2}m  {9,7:+0;-0;0}g  {10,7:+0;-0;0}g    {11,4:F0}g  {12}",
                    x.nome, x.duracao, x.alcance, x.pe, x.peAltura, x.altura, x.desvio, x.teto,
                    x.curso, x.giroNoImpacto, x.giroNoFim, x.torcao, Veredito(x)));

            int acerta = 0, chuta = 0, fora = 0;
            foreach (var x in m)
            {
                var v = Veredito(x);
                if (v.StartsWith("acerta")) acerta++;
                else if (v.StartsWith("CHUTE")) chuta++;   // a perna acerta; a lanca nao
                else fora++;
            }

            sb.AppendLine();
            sb.AppendLine("COMO LER");
            sb.AppendLine("  alcance   o quanto a PONTA avanca, no referencial do Sardael.");
            sb.AppendLine("  pe        o quanto o PE avanca, e pe.alt a altura dele nesse instante.");
            sb.AppendLine("            Pe adiantado E no ar = chute; adiantado e no chao = so' um passo.");
            sb.AppendLine("  altura    a altura da ponta no instante do alcance maximo: onde o golpe chega.");
            sb.AppendLine("  desvio    o quanto ela sai do plano nesse instante. O orc tem "
                        + RAIO_DO_ORC.ToString("F2") + " m de raio.");
            sb.AppendLine("  teto      altura maxima em todo o clipe: o tamanho da armada, que e' o telegrafo.");
            sb.AppendLine("  curso     o caminho percorrido pela ponta.");
            sb.AppendLine("  giro@imp  para onde o PEITO aponta NO IMPACTO, em graus da linha, COM SINAL.");
            sb.AppendLine("            Zero = de frente para o orc. Perto de 90 = de lado para a tela.");
            sb.AppendLine("            Acima de 90 = de costas para o orc.");
            sb.AppendLine("  giro@fim  onde o peito esta' no ultimo quadro: se o golpe devolve a guarda.");
            sb.AppendLine("  balanco   o pico da torcao no clipe. NAO reprova nada — todo golpe de haste");
            sb.AppendLine("            torce o tronco. Fica so' para mostrar o tamanho do gesto.");
            sb.AppendLine();
            sb.AppendLine(string.Format("  {0} acertam o corpo do orc.  {1} sao chute.  {2} passam por fora.",
                                        acerta, chuta, fora));
            if (faltando > 0) sb.AppendLine(string.Format("  ({0} clipe(s) nao encontrado(s).)", faltando));

            var csv = new StringBuilder();
            csv.AppendLine("clipe;unico;duracao;alcance;pe;pe_altura;altura;desvio;teto;curso;guarda;giro_impacto;giro_fim;balanco;impacto;avanco;veredito");
            foreach (var x in m)
                csv.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{0};{1};{2:F4};{3:F4};{4:F4};{5:F4};{6:F4};{7:F4};{8:F4};{9:F4};{10:F2};{11:F2};{12:F2};{13:F2};{14:F4};{15:F4};{16}",
                    x.nome, x.unico ? 1 : 0, x.duracao, x.alcance, x.pe, x.peAltura, x.altura,
                    x.desvio, x.teto, x.curso, x.guarda, x.giroNoImpacto, x.giroNoFim, x.torcao,
                    x.quando, x.avanco, Veredito(x)));

            string raiz = Application.dataPath + "/../";
            try { System.IO.File.WriteAllText(raiz + SAIDA, sb.ToString()); } catch { }
            try { System.IO.File.WriteAllText(raiz + CSV, csv.ToString()); } catch { }
            Debug.Log(sb.ToString() + "\ngravado em " + raiz + SAIDA + "  e  " + raiz + CSV);
        }
    }
}
