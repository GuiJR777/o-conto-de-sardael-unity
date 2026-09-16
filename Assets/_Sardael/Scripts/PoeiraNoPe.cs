using UnityEngine;

namespace Sardael
{
    /// <summary>
    /// Poeira no pe de quem anda.
    ///
    /// O emissor do pacote nao solta particula por TEMPO e sim por DISTANCIA percorrida
    /// (<c>rateOverDistance</c>). Isso resolve o problema todo sozinho: quem anda levanta
    /// poeira, quem para nao levanta, e ninguem precisa medir velocidade nem escrever codigo
    /// de passo. Basta o emissor ser filho de algo que se move.
    ///
    /// A simulacao roda em espaco de MUNDO, entao a nuvem fica pra tras no chao em vez de
    /// viajar grudada no personagem — e' o que faz parecer poeira e nao fumaca presa ao pe.
    ///
    /// Serve pra qualquer coisa que ande: heroi, aldeao em fuga, ovelha, carroca do comboio.
    /// Em quem nunca sai do lugar nao custa quadro nenhum, mas tambem nao adianta nada.
    /// </summary>
    [DisallowMultipleComponent]
    public class PoeiraNoPe : MonoBehaviour
    {
        [Header("De onde sai")]
        [Tooltip("O prefab do emissor. Se vazio, carrega de Resources/StepDust.")]
        public GameObject molde;
        [Tooltip("Altura acima da raiz. A raiz do personagem ja' fica no pe, entao um dedo "
               + "acima do chao basta pra nuvem nao nascer enterrada.")]
        public float altura = 0.06f;

        [Header("Cara da nuvem")]
        [Tooltip("Multiplica o tamanho que vem do prefab. 1 = do jeito que veio.")]
        public float tamanho = 0.7f;
        [Tooltip("Poeira de chao de terra. Branco puro le' como fumaca.")]
        public Color cor = new Color(0.87f, 0.81f, 0.70f, 1f);
        [Tooltip("Quantas particulas por metro andado. Zero mantem o valor do prefab.")]
        public float porMetro = 3.5f;
        [Tooltip("Segundos que cada nuvem dura. Zero mantem o do prefab.")]
        public float duracao = 0.9f;

        ParticleSystem emissor;

        public ParticleSystem Emissor { get { return emissor; } }

        void Start()
        {
            if (molde == null) molde = Resources.Load<GameObject>("StepDust");
            if (molde == null)
            {
                Debug.LogWarning("PoeiraNoPe em '" + name + "': nao achei o molde nem "
                               + "Resources/StepDust. Sem poeira.", this);
                enabled = false;
                return;
            }

            var go = Instantiate(molde, transform);
            go.name = "Poeira";
            go.transform.localPosition = new Vector3(0f, altura, 0f);
            go.transform.localRotation = Quaternion.identity;

            emissor = go.GetComponent<ParticleSystem>();
            if (emissor == null) { enabled = false; return; }
            Ajustar();
        }

        /// <summary>Aplica tamanho, cor e ritmo. Publico pra dar pra afinar em tempo de jogo.</summary>
        public void Ajustar()
        {
            if (emissor == null) return;

            var principal = emissor.main;
            if (tamanho > 0f)
            {
                var s = principal.startSize;
                s.constant = s.constant * tamanho;
                principal.startSize = s;
            }
            principal.startColor = cor;
            if (duracao > 0f) principal.startLifetime = duracao;

            if (porMetro > 0f)
            {
                var em = emissor.emission;
                em.rateOverDistance = porMetro;
            }
        }
    }
}
