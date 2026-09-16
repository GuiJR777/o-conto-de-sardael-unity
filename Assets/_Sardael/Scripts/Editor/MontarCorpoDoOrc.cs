using System.Text;
using UnityEngine;
using UnityEditor;

namespace SardaelEditor
{
    /// <summary>
    /// Da' corpo ao orc: uma capsula medida no rig dele, mais o Rigidbody que a faz ser um
    /// colisor que se MOVE em vez de um cenario parado.
    ///
    /// POR QUE ISTO EXISTE: o orc nao tinha colisor nenhum — so' malhas. O Sardael atravessava
    /// ele no fim da cadeia (a distancia chegava a -0,59 m, ou seja o heroi do outro lado), e
    /// nenhuma regra de acerto conserta isso: quem impede dois corpos de ocuparem o mesmo lugar
    /// e' corpo, nao conta de distancia.
    ///
    /// COMO A MEDIDA E' FEITA, E O QUE EU NAO USO
    /// -------------------------------------------
    /// O caminho obvio seria a caixa dos renderers. Ele esta' ERRADO aqui: a malha do prefab
    /// esta' na pose de bind, com os bracos abertos, entao a largura que sai de la' e' a
    /// ENVERGADURA — perto de dois metros. Uma capsula desse tamanho faria o orc empurrar o
    /// heroi de longe e nenhum golpe encostaria nunca.
    ///
    /// Entao a medida sai dos OSSOS do humanoide, que nao mentem sobre anatomia:
    ///   raio    metade da distancia entre os dois ombros (LeftUpperArm e RightUpperArm)
    ///   altura  do chao ate' o osso da cabeca, mais a calota do cranio
    /// Os dois numeros aparecem no relatorio junto com a envergadura, pra dar pra conferir de
    /// onde cada um veio.
    ///
    /// O RIGIDBODY CINEMATICO NAO E' ENFEITE: o CorpoDoOrc move o orc escrevendo em
    /// transform.position. Colisor sem Rigidbody que se mexe e' "static collider moved", e o
    /// PhysX reconstroi a arvore de colisao a cada quadro. Cinematico diz ao motor que este
    /// corpo se move por codigo, e o custo some.
    ///
    /// Rode quantas vezes quiser: se ja' houver capsula, ela e' remedida, nao duplicada.
    /// </summary>
    public static class MontarCorpoDoOrc
    {
        const string ORC = "Assets/_Sardael/Personagens/Orc.prefab";
        const string SAIDA = "_corpo_do_orc.txt";

        /// <summary>Do osso da cabeca ao alto do cranio. Nao da' pra tirar do rig: o osso e' o
        /// ponto de giro do pescoco, e o cranio continua acima dele.</summary>
        const float CALOTA = 0.13f;

        [MenuItem("Sardael/Montar Corpo do Orc (colisor)")]
        public static void Executar()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[Corpo] pare o Play primeiro."); return; }

            var molde = AssetDatabase.LoadAssetAtPath<GameObject>(ORC);
            if (molde == null) { Debug.LogError("[Corpo] nao achei " + ORC); return; }

            // mede numa instancia solta e destroi no finally: posar ou medir direto no prefab
            // aberto e' o caminho de deixar sujeira gravada nele
            float raio = 0f, altura = 0f, envergadura = 0f, cabeca = 0f, quadril = 0f;
            var corpo = (GameObject)PrefabUtility.InstantiatePrefab(molde);
            try
            {
                var anim = corpo.GetComponent<Animator>();
                if (anim == null || !anim.isHuman)
                { Debug.LogError("[Corpo] o Orc nao tem Animator humanoide; sem ossos nao ha' medida."); return; }

                var ombroE = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                var ombroD = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
                var maoE   = anim.GetBoneTransform(HumanBodyBones.LeftHand);
                var maoD   = anim.GetBoneTransform(HumanBodyBones.RightHand);
                var cab    = anim.GetBoneTransform(HumanBodyBones.Head);
                var quad   = anim.GetBoneTransform(HumanBodyBones.Hips);

                if (ombroE == null || ombroD == null || cab == null || quad == null)
                { Debug.LogError("[Corpo] faltam ossos no mapeamento humanoide do Orc."); return; }

                var paraLocal = corpo.transform.worldToLocalMatrix;
                Vector3 e = paraLocal.MultiplyPoint3x4(ombroE.position);
                Vector3 d = paraLocal.MultiplyPoint3x4(ombroD.position);
                raio = Vector3.Distance(e, d) * 0.5f;

                cabeca = paraLocal.MultiplyPoint3x4(cab.position).y;
                quadril = paraLocal.MultiplyPoint3x4(quad.position).y;
                altura = cabeca + CALOTA;

                if (maoE != null && maoD != null)
                    envergadura = Vector3.Distance(paraLocal.MultiplyPoint3x4(maoE.position),
                                                   paraLocal.MultiplyPoint3x4(maoD.position));
            }
            finally { Object.DestroyImmediate(corpo); }

            if (raio < 0.1f || raio > 0.8f || altura < 1.0f || altura > 3.0f)
            {
                Debug.LogError(string.Format(
                    "[Corpo] medida fora do razoavel: raio {0:F3} m, altura {1:F3} m. Nao vou "
                  + "gravar isso no prefab. Confira o mapeamento humanoide do Orc.", raio, altura));
                return;
            }

            // ------------------------------------------------ grava no prefab
            var conteudo = PrefabUtility.LoadPrefabContents(ORC);
            bool nasceu;
            try
            {
                var cap = conteudo.GetComponent<CapsuleCollider>();
                nasceu = cap == null;
                if (cap == null) cap = conteudo.AddComponent<CapsuleCollider>();
                cap.direction = 1;                                  // 1 = eixo Y, em pe'
                cap.radius = raio;
                cap.height = altura;
                cap.center = new Vector3(0f, altura * 0.5f, 0f);
                cap.isTrigger = false;

                var rb = conteudo.GetComponent<Rigidbody>();
                if (rb == null) rb = conteudo.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.interpolation = RigidbodyInterpolation.None;
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

                PrefabUtility.SaveAsPrefabAsset(conteudo, ORC);
            }
            finally { PrefabUtility.UnloadPrefabContents(conteudo); }

            Relatar(nasceu, raio, altura, envergadura, cabeca, quadril);
        }

        static void Relatar(bool nasceu, float raio, float altura, float envergadura,
                            float cabeca, float quadril)
        {
            var sb = new StringBuilder();
            sb.AppendLine("===== CORPO DO ORC =====");
            sb.AppendLine("Unity " + Application.unityVersion + "   " + System.DateTime.Now);
            sb.AppendLine();
            sb.AppendLine("MEDIDO nos ossos do humanoide, numa instancia solta que foi destruida depois.");
            sb.AppendLine();
            sb.AppendLine(string.Format("  raio da capsula      {0,6:F3} m   (metade da distancia entre os ombros)", raio));
            sb.AppendLine(string.Format("  altura da capsula    {0,6:F3} m   (osso da cabeca {1:F3} + calota {2:F2})", altura, cabeca, CALOTA));
            sb.AppendLine(string.Format("  quadril              {0,6:F3} m", quadril));
            sb.AppendLine();
            sb.AppendLine(string.Format("  envergadura (bracos) {0,6:F3} m   <- NAO usada, e por que:", envergadura));
            sb.AppendLine("     a malha do prefab esta' na pose de bind, de bracos abertos. Usar a caixa");
            sb.AppendLine("     dos renderers daria a capsula a largura dos bracos, e o orc empurraria o");
            sb.AppendLine("     heroi de longe demais pra qualquer golpe encostar.");
            sb.AppendLine();
            sb.AppendLine(nasceu ? "  CapsuleCollider CRIADO no Orc.prefab." : "  CapsuleCollider ja' existia: remedido.");
            sb.AppendLine("  Rigidbody cinematico garantido (colisor que se move por codigo).");
            sb.AppendLine();
            sb.AppendLine("O QUE ISSO MUDA NO COMBATE, E PRECISA SER MEDIDO DE NOVO");
            sb.AppendLine(string.Format(
                "  A capsula do Sardael tem raio 0,32 m. Com {0:F3} m no orc, os dois centros nao", raio));
            sb.AppendLine(string.Format(
                "  conseguem ficar a menos de {0:F2} m um do outro.", raio + 0.32f));
            sb.AppendLine("  A cadeia chegava a 0,58 m no terceiro elo. Se esse piso for maior que isso,");
            sb.AppendLine("  as distancias do teste MUDAM — e o teste de combate tem que rodar de novo");
            sb.AppendLine("  antes de qualquer conclusao sobre alcance continuar valendo.");

            string arq = Application.dataPath + "/../" + SAIDA;
            try { System.IO.File.WriteAllText(arq, sb.ToString()); } catch { }
            Debug.Log(sb.ToString() + "\ngravado em " + arq);
        }
    }
}
