using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Sardael.EditorFerramentas
{
    /// <summary>
    /// Poe a cena inteira no instante t e grava um lote de quadros.
    ///
    /// Reproduz a mao o que cada componente faria no Play: o <see cref="TocadorDeClipe"/> em
    /// laco, o <see cref="Combatente"/> na vez dele de bater ou defender, o
    /// <see cref="AldeaoEmFuga"/> correndo em volta de onde foi largado. Nao e' o jogo
    /// rodando, e' o jogo reconstruido quadro a quadro — o resultado na tela e' o mesmo.
    /// </summary>
    public static class DiretorDoVideo
    {
        struct Fugitivo { public Transform t; public Vector3 casa; public float fase, raio, vel; }
        static readonly List<Fugitivo> fugitivos = new List<Fugitivo>();
        static Terrain chao;
        static bool preparado;

        public static void Preparar(Transform raizDaCena)
        {
            GravadorDeQuadros.Guardar(raizDaCena);
            chao = Object.FindFirstObjectByType<Terrain>();
            fugitivos.Clear();
            int i = 0;
            foreach (var f in Object.FindObjectsByType<AldeaoEmFuga>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                fugitivos.Add(new Fugitivo {
                    t = f.transform, casa = f.transform.position,
                    fase = (i++) * 1.7f, raio = Mathf.Min(f.raio, 7f), vel = f.velocidade
                });
            }
            preparado = true;
        }

        public static void Encerrar() { GravadorDeQuadros.Devolver(); preparado = false; }

        public static void PosarTudo(float t)
        {
            if (!preparado) return;

            // --- quem tem um clipe so', em laco
            foreach (var tc in Object.FindObjectsByType<TocadorDeClipe>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (tc.clipe == null) continue;
                var an = tc.GetComponent<Animator>();
                float bruto = tc.atraso + t * Mathf.Max(0.01f, tc.velocidade);
                float tempo = tc.congelarNoFim
                    ? Mathf.Min(bruto, tc.clipe.length)
                    : bruto % Mathf.Max(0.01f, tc.clipe.length);
                GravadorDeQuadros.Pousar(an, tc.clipe, tempo);
            }

            // --- duelos: quem bate, quem defende, e quando
            foreach (var d in Object.FindObjectsByType<DueloEncenado>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (d.aliado == null || d.inimigo == null) continue;
                float local = t - d.atrasoInicial;
                if (local < 0f)
                {
                    Guarda(d.aliado, t); Guarda(d.inimigo, t);
                    continue;
                }
                var g0 = d.aliado.golpes != null && d.aliado.golpes.Length > 0 ? d.aliado.golpes[0] : null;
                float dur = g0 != null ? g0.length : 1.1f;
                float periodo = dur + d.pausa + d.variacao * 0.5f;
                int ciclo = Mathf.FloorToInt(local / periodo);
                float fase = local - ciclo * periodo;

                var atacante = (ciclo % 2 == 0) ? d.aliado : d.inimigo;
                var defensor = (ciclo % 2 == 0) ? d.inimigo : d.aliado;
                var golpe = atacante.golpes != null && atacante.golpes.Length > 0
                          ? atacante.golpes[Mathf.Abs(ciclo) % atacante.golpes.Length] : null;

                if (golpe != null && fase < golpe.length)
                    GravadorDeQuadros.Pousar(atacante.GetComponent<Animator>(), golpe, fase);
                else Guarda(atacante, t);

                float d0 = Mathf.Min(d.atrasoDaDefesa, (golpe != null ? golpe.length : dur) * 0.6f);
                if (defensor.defesa != null && fase >= d0 && fase < d0 + defensor.defesa.length)
                    GravadorDeQuadros.Pousar(defensor.GetComponent<Animator>(), defensor.defesa, fase - d0);
                else Guarda(defensor, t);
            }

            // --- quem foge, corre em volta de onde foi largado
            foreach (var f in fugitivos)
            {
                if (f.t == null || f.raio < 0.2f) continue;
                float ang = (f.fase + t * f.vel / f.raio);
                var p = f.casa + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * f.raio;
                if (chao != null) p.y = chao.SampleHeight(p) + chao.transform.position.y;
                var frente = new Vector3(-Mathf.Sin(ang), 0f, Mathf.Cos(ang));
                f.t.position = p;
                f.t.rotation = Quaternion.LookRotation(frente);
            }
        }

        static void Guarda(Combatente c, float t)
        {
            if (c == null || c.guarda == null) return;
            GravadorDeQuadros.Pousar(c.GetComponent<Animator>(), c.guarda,
                (t + c.GetInstanceIDEstavel() * 0.37f) % c.guarda.length);
        }

        /// <summary>Grava os quadros de <paramref name="de"/> a <paramref name="ate"/> (inclusive).</summary>
        public static int Lote(string pasta, GravadorDeQuadros.Marca[] trilho, Transform grupoDeFogo,
                               int de, int ate, float fps, int largura, int altura)
        {
            System.IO.Directory.CreateDirectory(pasta);
            int feitos = 0;
            for (int q = de; q <= ate; q++)
            {
                float t = q / fps;
                PosarTudo(t);
                GravadorDeQuadros.Fogo(grupoDeFogo, 1.2f + t);
                Vector3 onde; Quaternion rot; float fov;
                GravadorDeQuadros.NoTrilho(trilho, t, out onde, out rot, out fov);
                GravadorDeQuadros.Quadro(string.Format("{0}/q{1:0000}.png", pasta, q), onde, rot, fov, largura, altura);
                feitos++;
            }
            return feitos;
        }
    }

    static class Ajudas
    {
        // um numero estavel por componente, so' pra desencontrar a respiracao da guarda
        public static int GetInstanceIDEstavel(this Component c)
        {
            if (c == null) return 0;
            int h = c.name.GetHashCode();
            return Mathf.Abs(h % 17);
        }
    }
}
