using UnityEngine;
using UnityEditor;

namespace Sardael
{
    /// <summary>
    /// Poe uma arma na mao de um personagem da Synty no MESMO lugar em que o Kevin Iglesias
    /// animou a dele. Sem isso, cada arma vira tentativa e erro no inspetor.
    ///
    /// COMO O RIG DO KEVIN FUNCIONA: ele tem ossos de prop dedicados, B-handProp.L e
    /// B-handProp.R, e a arma entra ali com transform ZERADO. O osso ja' e' o ponto de pega,
    /// e ja' vem animado. Entao toda a informacao de "onde fica a arma" esta' na posicao
    /// desse osso em relacao a mao Humanoid — e isso da' pra transferir pra outro rig.
    ///
    /// COMO TRANSFERE: com os dois rigs na MESMA pose (T-pose), a mao dos dois aponta pro
    /// mesmo lado no mundo. Entao o deslocamento em espaco de MUNDO entre a mao e o osso de
    /// prop serve igual nos dois. Espaco local NAO serve: a Synty e o Kevin orientam o osso
    /// da mao de um jeito diferente, e a arma sairia torta.
    ///
    /// COMO ESCALA: a animacao varre um arco com um alcance especifico. O que importa nao e'
    /// o tamanho total da arma, e' quanto dela fica ACIMA DA MAO. Entao escala pelo total e
    /// depois escorrega pela haste ate' o alcance bater. Pra lanca isso e' o que salva: o
    /// pivo da lanca da Synty fica no meio do cabo, nao na pega.
    ///
    /// O IK HELPER TOOL do Kevin resolve o problema IRMAO deste: ele nao poe a arma na mao,
    /// ele puxa a MAO DE APOIO ate' a arma. Serve pra quando voce quiser de proposito uma
    /// arma fora de medida (um boss com machado gigante). Com as medidas certas aqui, a mao
    /// cai sozinha e o IK vira reserva.
    /// </summary>
    public static class EncaixeDeArma
    {
        const string DUMMY_KEVIN =
            "Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Melee Animations/" +
            "Prefabs/Characters/HumanM_Dummy_Red.prefab";

        public enum Lado { Direita, Esquerda }
        public enum Feitio { Haste, Placa }

        /// <summary>Medidas das armas do Kevin: e' o alvo que a animacao espera.</summary>
        public struct Medida
        {
            public float total, acimaDaMao, largura;
            public Feitio feitio;

            public static Medida Haste(float total, float acima)
            { return new Medida { total = total, acimaDaMao = acima, feitio = Feitio.Haste }; }

            public static Medida Placa(float largura)
            { return new Medida { largura = largura, feitio = Feitio.Placa }; }
        }

        // Medidas lidas dos prefabs de demo do Kevin. Nao mexer sem remedir.
        public static readonly Medida ESPADA    = Medida.Haste(1.280f, 1.048f);
        public static readonly Medida ADAGA     = Medida.Haste(0.816f, 0.613f);
        public static readonly Medida MALHO     = Medida.Haste(1.022f, 0.802f);
        public static readonly Medida MONTANTE  = Medida.Haste(1.940f, 1.585f);
        public static readonly Medida ALABARDA  = Medida.Haste(2.368f, 2.045f);
        public static readonly Medida ESCUDO    = Medida.Placa(0.631f);

        static bool medido;
        static Vector3 deslocD, deslocE;       // da mao ate' o osso de prop, em mundo, T-posado
        static Quaternion giroD, giroE;        // giro do osso de prop, em mundo, T-posado
        static float porteKevin = 1f;          // humanScale do dummy: a referencia de tamanho

        /// <summary>
        /// Le o rig do Kevin e guarda onde ficam os ossos de prop.
        /// TEM QUE SER CHAMADO ANTES de abrir o seu proprio bloco de AnimationMode — ele abre
        /// e fecha um bloco proprio, e fechar reverte quem nao foi amostrado dentro dele.
        /// </summary>
        public static void Preparar(AnimationClip tpose)
        {
            if (medido) return;

            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(DUMMY_KEVIN);
            if (pf == null) { Debug.LogError("EncaixeDeArma: nao achei o dummy do Kevin."); return; }

            var dummy = (GameObject)PrefabUtility.InstantiatePrefab(pf);
            dummy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var an = dummy.GetComponentInChildren<Animator>(true);

            AnimationMode.StartAnimationMode();
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(an.gameObject, tpose, 0f);
            AnimationMode.EndSampling();

            var maoD = an.GetBoneTransform(HumanBodyBones.RightHand);
            var maoE = an.GetBoneTransform(HumanBodyBones.LeftHand);
            var propD = Procurar(dummy.transform, "B-handProp.R");
            var propE = Procurar(dummy.transform, "B-handProp.L");

            deslocD = propD.position - maoD.position; giroD = propD.rotation;
            deslocE = propE.position - maoE.position; giroE = propE.rotation;
            porteKevin = an.humanScale;

            AnimationMode.StopAnimationMode();
            Object.DestroyImmediate(dummy);
            medido = true;
        }

        /// <summary>
        /// Encaixa a arma. O personagem PRECISA estar T-posado no momento da chamada
        /// (dentro do seu bloco de AnimationMode), senao o encaixe sai na pose errada.
        /// </summary>
        public static GameObject Montar(Animator alvo, GameObject prefabArma, Lado lado,
                                        Medida medida, string nome)
        {
            if (!medido) { Debug.LogError("EncaixeDeArma: chame Preparar() antes."); return null; }
            if (prefabArma == null || alvo == null) return null;

            bool dir = lado == Lado.Direita;
            var mao = alvo.GetBoneTransform(dir ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            if (mao == null) { Debug.LogError("EncaixeDeArma: rig sem mao " + lado); return null; }

            // PORTE: as medidas do Kevin valem pra um corpo de 1,85 m. Num minotauro de 3,9 m
            // o mesmo montante vira palito. O humanScale do Animator e' exatamente a razao de
            // tamanho que o retargeting Humanoid ja' usa — entao a arma cresce pelo mesmo
            // numero, e a mao continua caindo no cabo. Num orc (quase do tamanho do dummy)
            // o fator fica ~1 e nada muda.
            float porte = porteKevin > 0.0001f && alvo.humanScale > 0.0001f
                        ? alvo.humanScale / porteKevin : 1f;

            var desloc = (dir ? deslocD : deslocE) * porte;
            var giro = dir ? giroD : giroE;

            medida.total *= porte;
            medida.acimaDaMao *= porte;
            medida.largura *= porte;

            var malha = Caixa(prefabArma);

            // CONVENCAO DA LAMINA: em toda arma do Kevin o lado LARGO da lamina esta' no X
            // local e a chapa fina no Z. Varias armas da Synty vem ao contrario. Se entrar
            // assim, o alcance bate certinho mas o golpe corta de chapa em vez de fio —
            // erro que numero nenhum de comprimento denuncia. Gira 90 no eixo da haste.
            // O giro e' em torno do proprio Y, entao nao mexe em quanto a arma alcanca.
            float correcao = malha.size.x >= malha.size.z ? 0f : 90f;
            giro = giro * Quaternion.Euler(0f, correcao, 0f);

            var arma = (GameObject)PrefabUtility.InstantiatePrefab(prefabArma, mao);
            arma.name = string.IsNullOrEmpty(nome) ? prefabArma.name : nome;
            arma.transform.rotation = giro;

            float escala;
            Vector3 pos = mao.position + desloc;

            if (medida.feitio == Feitio.Placa)
            {
                // escudo: casa a largura e centra a chapa onde o Kevin centrou a dele
                float largura = Mathf.Max(malha.size.x, malha.size.y);
                escala = medida.largura / Mathf.Max(0.0001f, largura);
                pos -= giro * (malha.center * escala);
            }
            else
            {
                // haste: escala pelo total, depois escorrega pelo eixo ate' o alcance bater
                escala = medida.total / Mathf.Max(0.0001f, malha.size.y);
                float sobra = medida.acimaDaMao - malha.max.y * escala;
                pos += (giro * Vector3.up) * sobra;
            }

            arma.transform.position = pos;

            // o osso da Synty carrega lossyScale 0.01 vindo do FBX: sem dividir, a arma
            // aparece 100x menor e some dentro da mao
            var e = mao.lossyScale;
            arma.transform.localScale = new Vector3(escala / e.x, escala / e.y, escala / e.z);
            return arma;
        }

        /// <summary>Quanto da arma ficou acima da mao, pra conferir contra o alvo.</summary>
        public static float AlcanceAcimaDaMao(GameObject arma, Animator alvo, Lado lado)
        {
            var mao = alvo.GetBoneTransform(lado == Lado.Direita
                ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            float porte = porteKevin > 0.0001f && alvo.humanScale > 0.0001f
                        ? alvo.humanScale / porteKevin : 1f;
            var desloc = (lado == Lado.Direita ? deslocD : deslocE) * porte;
            var giro = lado == Lado.Direita ? giroD : giroE;
            var malha = Caixa(arma);
            var pega = mao.position + desloc;
            var ponta = arma.transform.position + (giro * Vector3.up) * (malha.max.y * arma.transform.lossyScale.y);
            return Vector3.Distance(pega, ponta);
        }

        static Bounds Caixa(GameObject g)
        {
            var mf = g.GetComponentInChildren<MeshFilter>(true);
            return mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds();
        }

        static Transform Procurar(Transform raiz, string nome)
        {
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                if (t.name == nome) return t;
            return null;
        }
    }
}
