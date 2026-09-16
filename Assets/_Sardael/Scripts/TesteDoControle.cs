using UnityEngine;
using System.Text;

namespace Sardael
{
    /// <summary>
    /// Aperta as teclas sozinho e anota o que o Animator respondeu.
    ///
    /// Entra pelas MESMAS variaveis que o teclado de verdade (os Injetar* do
    /// <see cref="MovimentoDoHeroi"/>), nao por um caminho paralelo: se este teste passa, e'
    /// porque o caminho do jogador passa. Testar por um atalho que o jogador nao usa nao
    /// prova nada.
    /// </summary>
    public class TesteDoControle : MonoBehaviour
    {
        public MovimentoDoHeroi mov;
        public string arquivo = "";
        public float intervalo = 0.2f;
        [Tooltip("Camera que acompanha. Sem isso ele sai do quadro no primeiro segundo de corrida.")]
        public Transform camera;
        public Vector3 deslocamentoDaCamera = new Vector3(0f, 2.4f, -9f);

        float proximo;
        readonly StringBuilder texto = new StringBuilder();
        Animator anim;

        void Start()
        {
            if (mov == null) mov = GetComponent<MovimentoDoHeroi>();
            anim = GetComponent<Animator>();
            if (string.IsNullOrEmpty(arquivo)) arquivo = Application.dataPath + "/../_teste_controle.txt";
            texto.Append("t\troteiro\testado\tvelocidade\tx\tnoChao\n");
        }

        void Update()
        {
            float t = Time.timeSinceLevelLoad;
            string etapa;

            // o roteiro: parado, andar, correr, pular correndo, parar
            if (t < 1.5f) { etapa = "parado"; }
            else if (t < 4.5f) { etapa = "andando"; mov.InjetarEixo(1f); }
            else if (t < 8.0f) { etapa = "correndo"; mov.InjetarEixo(1f); mov.InjetarCorrer(true); }
            else if (t < 8.1f) { etapa = "PULOU"; mov.InjetarEixo(1f); mov.InjetarCorrer(true); mov.InjetarPulo(); }
            else if (t < 11f) { etapa = "no ar / caindo"; mov.InjetarEixo(1f); mov.InjetarCorrer(true); }
            else if (t < 13f) { etapa = "voltando a andar"; mov.InjetarEixo(-1f); }
            else { etapa = "parado de novo"; mov.InjetarEixo(0f); mov.InjetarCorrer(false); }

            if (t < proximo) return;
            proximo = t + intervalo;

            texto.Append(t.ToString("F2")).Append('\t')
                 .Append(etapa).Append('\t')
                 .Append(mov.EstadoDaAnimacao).Append('\t')
                 .Append(mov.VelocidadeAtual.ToString("F2")).Append('\t')
                 .Append(transform.position.x.ToString("F2")).Append('\t')
                 .Append(mov.NoChao).Append('\n');
        }

        void LateUpdate()
        {
            if (camera == null) return;
            // acompanha so' em X: a linha 2.5D nao tem profundidade pra seguir, e girar a camera
            // junto quando ele vira faria o cenario dar meia-volta no meio do teste
            var p = transform.position + deslocamentoDaCamera;
            camera.position = Vector3.Lerp(camera.position, p, 1f - Mathf.Exp(-8f * Time.deltaTime));
            camera.LookAt(transform.position + Vector3.up * 1.1f);
        }

        void OnDisable() { Gravar(); }
        void OnApplicationQuit() { Gravar(); }

        void Gravar()
        {
            if (texto.Length == 0) return;
            System.IO.File.WriteAllText(arquivo, texto.ToString());
            Debug.Log("teste do controle gravado em " + arquivo);
        }
    }
}
