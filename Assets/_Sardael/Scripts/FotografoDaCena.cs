using UnityEngine;
using UnityEngine.Rendering;

namespace Sardael
{
    /// <summary>
    /// Tira fotos da cena em instantes marcados, de dentro do Play.
    ///
    /// Existe porque conferir cutscene por fora nao funciona: cada ida e volta ao editor leva
    /// segundos e o momento certo ja' passou. Aqui os instantes sao numeros, o resultado e'
    /// sempre o mesmo, e a malha esfolada esta' correta porque quem esta' desenhando e' o
    /// proprio jogo — nao uma renderizacao forcada no editor, que reaproveita as matrizes de
    /// osso do quadro anterior e devolve a pose errada.
    /// </summary>
    public class FotografoDaCena : MonoBehaviour
    {
        [Tooltip("Instantes, em segundos desde o inicio do Play.")]
        public float[] instantes = { 3f, 8f, 14f, 19f, 23f, 26f, 29f };
        public string prefixo = "morte";
        public string pasta = "";
        public int largura = 1280, altura = 720;
        [Tooltip("Qual camera retratar. Vazio = a principal.")]
        public Camera camera;

        int proxima;
        Camera cam;

        void Start()
        {
            // Camera.main olha a etiqueta MainCamera, e numa cutscene dentro de Halu a camera do
            // jogo continua sendo a etiquetada: sem este campo o fotografo retrata a aldeia
            // inteira e nao o plano da cena. Aconteceu, e as seis fotos vieram do lugar errado.
            cam = camera != null ? camera : Camera.main;
            if (string.IsNullOrEmpty(pasta)) pasta = Application.dataPath + "/../_fotos/";
            System.IO.Directory.CreateDirectory(pasta);
        }

        void LateUpdate()
        {
            if (cam == null || proxima >= instantes.Length) return;
            if (Time.timeSinceLevelLoad < instantes[proxima]) return;

            var rt = new RenderTexture(largura, altura, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            var alvoAntigo = cam.targetTexture;
            cam.targetTexture = rt;

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

            string arq = pasta + prefixo + "_" + (proxima + 1).ToString("00") + "_"
                       + instantes[proxima].ToString("F0") + "s.png";
            System.IO.File.WriteAllBytes(arq, tex.EncodeToPNG());
            Destroy(tex);
            rt.Release();
            Destroy(rt);

            Debug.Log("foto " + arq);
            proxima++;
        }
    }
}
