using UnityEngine;
using UnityEngine.Rendering;

namespace Sardael
{
    /// <summary>
    /// Leva uma camera por uma lista de pontos e fotografa cada um, de dentro do Play.
    ///
    /// Conferir cenario pelo editor engana: particula parada, personagem em pose de bind,
    /// vento sem soprar. O que o jogador ve' so' existe rodando. Este passeio da' uma volta
    /// pela cena inteira numa unica entrada em Play e sai sozinho no fim — assim a conferencia
    /// cabe num comando so', sem ficar entrando e saindo do modo de jogo.
    ///
    /// O <see cref="FotografoDaCena"/> irmao fotografa UMA camera em instantes marcados; este
    /// aqui troca de ponto de vista. Os dois existem porque cutscene e cenario se conferem de
    /// jeitos diferentes.
    /// </summary>
    public class PasseioDaCena : MonoBehaviour
    {
        [System.Serializable]
        public class Parada
        {
            public string nome = "ponto";
            public Vector3 onde;
            public Vector3 paraOnde;
            public float campoDeVisao = 45f;
        }

        public Parada[] paradas = new Parada[0];
        [Tooltip("Segundos parado em cada ponto antes de disparar. Da' tempo da particula "
               + "encher e do passo da animacao sair da pose zero.")]
        public float esperaPorParada = 1.5f;
        public string pasta = "";
        public string prefixo = "cena";
        public int largura = 1280, altura = 720;
        [Tooltip("Sai do modo de jogo quando a ultima foto sair.")]
        public bool sairNoFim = true;

        Camera cam;
        int proxima;
        float trocouEm;
        bool acabou;

        void Start()
        {
            if (string.IsNullOrEmpty(pasta)) pasta = Application.dataPath + "/../_fotos/";
            System.IO.Directory.CreateDirectory(pasta);

            var go = new GameObject("__camera_do_passeio");
            cam = go.AddComponent<Camera>();
            cam.nearClipPlane = 0.15f;
            cam.farClipPlane = 1200f;
            cam.depth = 100;                 // por cima da camera do jogo, senao a foto sai da outra
            Posicionar();
            trocouEm = Time.timeSinceLevelLoad;
        }

        void Posicionar()
        {
            if (proxima >= paradas.Length) return;
            var p = paradas[proxima];
            cam.transform.position = p.onde;
            cam.transform.rotation = Quaternion.Euler(p.paraOnde);
            cam.fieldOfView = p.campoDeVisao;
        }

        void LateUpdate()
        {
            if (acabou || cam == null || proxima >= paradas.Length) return;
            if (Time.timeSinceLevelLoad - trocouEm < esperaPorParada) return;

            Gravar(paradas[proxima]);
            proxima++;
            if (proxima < paradas.Length)
            {
                Posicionar();
                trocouEm = Time.timeSinceLevelLoad;
                return;
            }

            acabou = true;
            Debug.Log("passeio terminado: " + paradas.Length + " fotos em " + pasta);
#if UNITY_EDITOR
            if (sairNoFim) UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        void Gravar(Parada p)
        {
            var rt = new RenderTexture(largura, altura, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            var alvoAntigo = cam.targetTexture;
            cam.targetTexture = rt;

            // no URP o Render() cru pula parte da pilha; o pedido padrao desenha igual ao jogo
            var req = new RenderPipeline.StandardRequest();
            req.destination = rt;
            if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
            else cam.Render();

            var antes = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(largura, altura, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, largura, altura), 0, 0);
            tex.Apply();
            RenderTexture.active = antes;
            cam.targetTexture = alvoAntigo;

            string arq = pasta + prefixo + "_" + (proxima + 1).ToString("00") + "_" + p.nome + ".png";
            System.IO.File.WriteAllBytes(arq, tex.EncodeToPNG());
            Destroy(tex);
            rt.Release();
            Destroy(rt);
            Debug.Log("foto " + arq);
        }
    }
}
