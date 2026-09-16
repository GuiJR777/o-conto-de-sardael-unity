using UnityEngine;
using System.Text;

namespace Sardael
{
    // Anota, quadro a quadro dentro do Play, para onde o CORPO do veado aponta no mundo e se o
    // casco apoiado esta' deslizando.
    //
    // Medir isso fora do Play nao serve: sem o Animator rodando de verdade os ossos ficam na
    // pose de bind e a leitura sai limpa e errada. Aqui o que se le e' o mesmo que a camera ve.
    //
    // O eixo do corpo e' pelvis -> peito. Nao uso a cabeca porque ela gira sozinha nos clipes
    // (o veado olha em volta), e nao uso o transform da raiz porque ele nunca gira - e' o
    // script da cena que empurra a posicao, entao a raiz sempre leria 0 grau.
    //
    // As colunas de rotacao existem para separar culpa: se o angulo do corpo muda e nenhum dos
    // transforms mudou junto, quem girou foi a animacao; se o Modelo mudou, foi o Animator
    // reescrevendo o objeto que carrega ele - era esse o defeito do andar de lado.
    public class VigiaDoVeado : MonoBehaviour
    {
        public Transform quadril, peito;
        public Transform modelo, raizDoRig;
        public Transform[] cascos;
        public Animator animador;
        public CenaDaMorte cena;
        public string arquivo = "";
        public float intervalo = 0.25f;

        float proximo;
        readonly StringBuilder texto = new StringBuilder();

        // o casco apoiado e' o mais baixo do quadro; se ele nao estiver parado no chao, desliza
        Vector3 cascoAntes;
        Transform qualCasco;
        float somaDeslize, somaAmostras;

        void Start()
        {
            if (string.IsNullOrEmpty(arquivo)) arquivo = Application.dataPath + "/../_vigia_veado.txt";
            texto.Append("t\tetapa\tangulo\tleitura\tVeado.y\tModelo.y\tdeslize m/s\tclipe\n");
        }

        void LateUpdate()
        {
            MedirDeslize();

            if (quadril == null || peito == null) return;
            if (Time.timeSinceLevelLoad < proximo) return;
            proximo = Time.timeSinceLevelLoad + intervalo;

            var eixo = peito.position - quadril.position;
            eixo.y = 0f;
            if (eixo.sqrMagnitude < 1e-6f) return;
            eixo.Normalize();

            // 0 = aponta pro fundo (+Z), 90 = pra direita da tela (+X), -90 = pra esquerda
            float ang = Mathf.Atan2(eixo.x, eixo.z) * Mathf.Rad2Deg;
            float a = Mathf.Abs(ang);

            string leitura;
            if (a > 150f) leitura = "DE FRENTE PRA CAMERA";
            else if (a < 30f) leitura = "DE COSTAS PRA CAMERA";
            else if (ang < 0f) leitura = "PERFIL pra ESQUERDA (certo)";
            else leitura = "PERFIL pra DIREITA";

            string clipe = "";
            if (animador != null)
                foreach (var c in animador.GetCurrentAnimatorClipInfo(0))
                    if (c.clip != null) clipe += c.clip.name + " x" + c.weight.ToString("F2") + " ";
            if (clipe.Length == 0) clipe = "(nenhum)";

            float deslize = somaAmostras > 0f ? somaDeslize / somaAmostras : 0f;
            somaDeslize = 0f; somaAmostras = 0f;

            texto.Append(Time.timeSinceLevelLoad.ToString("F2")).Append('\t')
                 .Append(cena == null ? "?" : cena.Agora.ToString()).Append('\t')
                 .Append(ang.ToString("F0")).Append('\t')
                 .Append(leitura).Append('\t')
                 .Append(transform.eulerAngles.y.ToString("F0")).Append('\t')
                 .Append(modelo == null ? "-" : modelo.eulerAngles.y.ToString("F0")).Append('\t')
                 .Append(deslize.ToString("F2")).Append('\t')
                 .Append(clipe).Append('\n');
        }

        // Casco parado no chao = velocidade zero no mundo. Se o script empurra o corpo mais
        // rapido (ou mais devagar) que a passada do clipe, o casco apoiado ganha velocidade e
        // o bicho patina. O numero que sai daqui e' o erro, em m/s.
        void MedirDeslize()
        {
            if (cascos == null || cascos.Length == 0 || Time.deltaTime <= 0f) return;

            Transform baixo = null;
            foreach (var c in cascos)
                if (c != null && (baixo == null || c.position.y < baixo.position.y)) baixo = c;
            if (baixo == null) return;

            // so' vale quando e' O MESMO casco de novo. Na troca de apoio o "mais baixo" pula de
            // uma pata pra outra, e a diferenca entre dois ossos diferentes vira um deslize
            // fantasma de 3 m/s que nao existe na tela.
            if (qualCasco == baixo)
            {
                var d = baixo.position - cascoAntes;
                d.y = 0f;
                somaDeslize += d.magnitude / Time.deltaTime;
                somaAmostras += 1f;
            }
            cascoAntes = baixo.position;
            qualCasco = baixo;
        }

        void OnDisable() { Gravar(); }
        void OnApplicationQuit() { Gravar(); }

        void Gravar()
        {
            if (texto.Length == 0) return;
            System.IO.File.WriteAllText(arquivo, texto.ToString());
            Debug.Log("vigia gravado em " + arquivo);
        }
    }
}
