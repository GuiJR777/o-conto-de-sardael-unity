using UnityEngine;

namespace Sardael
{
    /// <summary>
    /// Aldeao correndo de verdade, e nao correndo parado.
    ///
    /// O clipe de corrida do pacote e' "in place": os pes andam, o corpo fica. Quem tem que
    /// levar o corpo e' este script — e na velocidade EXATA da passada, senao o pe' patina.
    /// <see cref="velocidade"/> ja' vem no valor medido do HumanM@Run01_Forward (3,17 m/s),
    /// o mesmo que o blend tree de Sardael usa com fator 1,00.
    ///
    /// Ele corre em volta de <see cref="casa"/>, que e' gravado onde voce largou o aldeao na
    /// cena. E' a mesma regra da <see cref="Ovelha"/>: quem foge sai correndo, mas nao sai da
    /// fazenda — senao em meio minuto de jogo o mapa esvazia.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class AldeaoEmFuga : MonoBehaviour
    {
        [Header("Territorio")]
        [Tooltip("Ate onde ele se afasta do ponto em que foi posto.")]
        public float raio = 9f;
        [Tooltip("Recusa destino com desnivel maior que isto: e' o que o mantem fora de barranco.")]
        public float desnivelMaximo = 1.6f;

        [Header("Corrida")]
        [Tooltip("Metros por segundo. 3,17 e' a passada do Run01_Forward sem deslizar.")]
        public float velocidade = 3.17f;
        public float aceleracao = 9f;
        public float giroPorSegundo = 320f;
        [Tooltip("Chegou a esta distancia do destino, escolhe outro.")]
        public float tolerancia = 1.1f;

        [Header("Folego")]
        [Tooltip("De vez em quando para pra olhar pra tras. Zero = nunca para.")]
        public Vector2 tempoParado = new Vector2(0.6f, 1.6f);
        [Range(0f, 1f)] public float chanceDeParar = 0.25f;

        Terrain chao;
        Vector3 casa, destino;
        float alturaRelativa, vel, ateQuando;
        bool parado;

        void Awake()
        {
            chao = Terrain.activeTerrain;
            casa = transform.position;
            var p = transform.position;
            alturaRelativa = chao != null ? p.y - AlturaDoChao(p) : 0f;
        }

        void Start()
        {
            EscolherDestino();
            // cada um sai num instante diferente; largar cinco juntos denuncia o script
            ateQuando = Time.time + Random.Range(0f, 1.2f);
        }

        void Update()
        {
            float dt = Time.deltaTime;

            if (parado)
            {
                if (Time.time >= ateQuando) { parado = false; EscolherDestino(); }
            }
            else
            {
                var plano = destino - transform.position;
                plano.y = 0f;
                if (plano.magnitude < tolerancia)
                {
                    if (Random.value < chanceDeParar)
                    {
                        parado = true;
                        ateQuando = Time.time + Random.Range(tempoParado.x, tempoParado.y);
                    }
                    else EscolherDestino();
                }
                else
                {
                    var alvo = Quaternion.LookRotation(plano.normalized, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, alvo, giroPorSegundo * dt);
                }
            }

            vel = Mathf.MoveTowards(vel, parado ? 0f : velocidade, aceleracao * dt);
            if (vel > 0.01f) transform.position += transform.forward * vel * dt;
            Colar();
        }

        void EscolherDestino()
        {
            float alturaCasa = AlturaDoChao(casa);
            for (int i = 0; i < 8; i++)
            {
                var c = Random.insideUnitCircle * raio;
                var p = new Vector3(casa.x + c.x, 0f, casa.z + c.y);
                if ((p - transform.position).sqrMagnitude < 9f) continue;   // destino perto demais vira ziguezague
                if (Mathf.Abs(AlturaDoChao(p) - alturaCasa) > desnivelMaximo) continue;
                p.y = transform.position.y;
                destino = p;
                return;
            }
            destino = casa;
        }

        float AlturaDoChao(Vector3 p)
        {
            if (chao == null) return p.y;
            return chao.SampleHeight(p) + chao.transform.position.y;
        }

        void Colar()
        {
            if (chao == null) return;
            var p = transform.position;
            p.y = AlturaDoChao(p) + alturaRelativa;
            transform.position = p;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.95f, 0.7f, 0.2f, 0.7f);
            Gizmos.DrawWireSphere(Application.isPlaying ? casa : transform.position, raio);
        }
    }
}
