using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Sardael.EditorFerramentas
{
    /// <summary>
    /// Grava quadros da cena SEM entrar em Play — porque o MCP nao deixa entrar.
    ///
    /// O truque: um <see cref="PlayableGraph"/> pode ser avaliado a mao no editor
    /// (<c>graph.Evaluate()</c>), que e' o que o Timeline faz no preview dele. Assim eu ponho
    /// cada personagem no tempo t do clipe dele, tiro a foto, avanco, e no fim devolvo todos
    /// os ossos exatamente onde estavam.
    ///
    /// Devolver e' o ponto: posar rig no editor grava a pose inteira na cena. Por isso
    /// <see cref="Guardar"/> anota localPosition/localRotation/localScale de TODO transform
    /// tocado, e <see cref="Devolver"/> repoe antes de eu sair.
    /// </summary>
    public static class GravadorDeQuadros
    {
        class Pose { public Transform t; public Vector3 p; public Quaternion r; public Vector3 e; }

        static readonly List<Pose> guardadas = new List<Pose>();
        static readonly List<PlayableGraph> grafos = new List<PlayableGraph>();
        static readonly Dictionary<Animator, AnimationClipPlayable> canais = new Dictionary<Animator, AnimationClipPlayable>();
        // Animator com controller ignora o grafo de fora e fica na pose de bind. Sardael tem
        // controller e os NPCs nao — por isso so' ele saia em T. Guardo, tiro, e devolvo no fim.
        static readonly Dictionary<Animator, RuntimeAnimatorController> controllers = new Dictionary<Animator, RuntimeAnimatorController>();
        // Mexer osso fora do Play NAO suja a matriz de pele sozinho: o SkinnedMeshRenderer
        // desenha a pose do primeiro Evaluate daquele grafo e trava ali, mesmo com os ossos
        // andando. Da' o boneco em pe' na foto e deitado na medicao. Este e' o interruptor
        // que existe pra isso; devolvo no Devolver.
        static readonly Dictionary<SkinnedMeshRenderer, bool> matrizes = new Dictionary<SkinnedMeshRenderer, bool>();

        // ------------------------------------------------------------------ estado
        public static void Guardar(Transform raiz)
        {
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                guardadas.Add(new Pose { t = t, p = t.localPosition, r = t.localRotation, e = t.localScale });
        }

        public static void Devolver()
        {
            foreach (var g in grafos) if (g.IsValid()) g.Destroy();
            grafos.Clear(); canais.Clear();
            foreach (var kv in controllers) if (kv.Key != null) kv.Key.runtimeAnimatorController = kv.Value;
            controllers.Clear();
            foreach (var kv in matrizes) if (kv.Key != null) kv.Key.forceMatrixRecalculationPerRender = kv.Value;
            matrizes.Clear();
            for (int i = guardadas.Count - 1; i >= 0; i--)
            {
                var q = guardadas[i];
                if (q.t == null) continue;
                q.t.localPosition = q.p; q.t.localRotation = q.r; q.t.localScale = q.e;
            }
            guardadas.Clear();
        }

        // ------------------------------------------------------------------ pose
        /// <summary>
        /// Poe um Animator no instante <paramref name="tempo"/> de um clipe.
        ///
        /// NUNCA chame <c>Animator.Rebind()</c> em edicao antes disto. O Rebind derruba de vez
        /// o vinculo do AnimationPlayableOutput: dali pra frente o Evaluate roda sem erro e o
        /// osso nao mexe, pelo resto da sessao do editor. Religar o objeto nao recupera —
        /// so' recarregar a cena. Pra ter a bind pose de referencia, meca ANTES de posar.
        /// </summary>
        public static void Pousar(Animator anim, AnimationClip clipe, float tempo)
        {
            if (anim == null || clipe == null) return;
            if (anim.runtimeAnimatorController != null)
            {
                if (!controllers.ContainsKey(anim)) controllers[anim] = anim.runtimeAnimatorController;
                anim.runtimeAnimatorController = null;
            }
            foreach (var malha in anim.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (matrizes.ContainsKey(malha)) continue;
                matrizes[malha] = malha.forceMatrixRecalculationPerRender;
                malha.forceMatrixRecalculationPerRender = true;
            }

            AnimationClipPlayable canal;
            if (!canais.TryGetValue(anim, out canal) || !canal.IsValid()
                || canal.GetAnimationClip() != clipe)
            {
                var grafo = PlayableGraph.Create("Gravador_" + anim.name);
                grafo.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var saida = AnimationPlayableOutput.Create(grafo, "saida", anim);
                canal = AnimationClipPlayable.Create(grafo, clipe);
                canal.SetApplyFootIK(false);
                saida.SetSourcePlayable(canal);
                grafos.Add(grafo);
                canais[anim] = canal;
            }
            canal.SetTime(tempo);
            canal.GetGraph().Evaluate();
        }

        // ------------------------------------------------------------------ foto
        static Camera cam;
        public static void AbrirCamera(int largura, int altura)
        {
            var go = new GameObject("__camera_do_gravador");
            go.hideFlags = HideFlags.HideAndDontSave;
            cam = go.AddComponent<Camera>();
            cam.nearClipPlane = 0.12f;
            cam.farClipPlane = 1400f;
        }

        public static void FecharCamera()
        {
            if (cam != null) Object.DestroyImmediate(cam.gameObject);
            cam = null;
        }

        public static void Quadro(string arquivo, Vector3 onde, Quaternion paraOnde, float fov, int w, int h)
        {
            if (cam == null) AbrirCamera(w, h);
            cam.transform.position = onde;
            cam.transform.rotation = paraOnde;
            cam.fieldOfView = fov;

            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 2;
            cam.targetTexture = rt;
            cam.Render();
            var antes = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = antes;
            System.IO.File.WriteAllBytes(arquivo, tex.EncodeToPNG());
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }

        // ------------------------------------------------------------------ trilho de camera
        public struct Marca
        {
            public float t; public Vector3 onde; public Vector3 mira; public float fov;
            public Marca(float t, Vector3 onde, Vector3 mira, float fov)
            { this.t = t; this.onde = onde; this.mira = mira; this.fov = fov; }
        }

        /// <summary>Posicao e mira da camera no instante t, interpolando as marcas com suavizacao.</summary>
        public static void NoTrilho(Marca[] marcas, float t, out Vector3 onde, out Quaternion rot, out float fov)
        {
            if (marcas == null || marcas.Length == 0)
            { onde = Vector3.zero; rot = Quaternion.identity; fov = 45f; return; }
            if (t <= marcas[0].t) { onde = marcas[0].onde; rot = Quaternion.LookRotation(marcas[0].mira - onde); fov = marcas[0].fov; return; }
            var ult = marcas[marcas.Length - 1];
            if (t >= ult.t) { onde = ult.onde; rot = Quaternion.LookRotation(ult.mira - onde); fov = ult.fov; return; }

            for (int i = 0; i < marcas.Length - 1; i++)
            {
                var a = marcas[i]; var b = marcas[i + 1];
                if (t < a.t || t > b.t) continue;
                float u = Mathf.InverseLerp(a.t, b.t, t);
                u = u * u * (3f - 2f * u);                 // entra e sai macio, sem tranco
                onde = Vector3.Lerp(a.onde, b.onde, u);
                var mira = Vector3.Lerp(a.mira, b.mira, u);
                fov = Mathf.Lerp(a.fov, b.fov, u);
                var d = mira - onde;
                rot = d.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(d) : Quaternion.identity;
                return;
            }
            onde = ult.onde; rot = Quaternion.LookRotation(ult.mira - onde); fov = ult.fov;
        }

        // ------------------------------------------------------------------ particulas
        public static void Fogo(Transform grupo, float tempo)
        {
            if (grupo == null) return;
            foreach (var ps in grupo.GetComponentsInChildren<ParticleSystem>(true))
                ps.Simulate(tempo, true, true, false);
        }
    }
}
