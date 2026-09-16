using UnityEngine;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Sardael
{
    /// <summary>
    /// Camera livre para inspecionar cenas de teste.
    ///
    /// O SimpleCameraController que vem no pacote de animacao usa a API velha
    /// (UnityEngine.Input), e este projeto esta' configurado so' com o Input System novo
    /// (activeInputHandler = 1). Naquele modo a API velha lanca excecao e a camera nao
    /// responde a nada. Este script le pelos dois caminhos, entao funciona com qualquer
    /// configuracao de entrada.
    ///
    /// WASD   andar          Q / E    descer / subir
    /// botao direito + mouse   olhar em volta
    /// Shift  correr         roda do mouse   ajusta a velocidade
    /// Tab    pula pra proxima luta   (Shift+Tab volta)
    /// F      reenquadra a luta atual
    /// 1..6   salta para uma area da cena
    /// </summary>
    public class CameraLivre : MonoBehaviour
    {
        [Header("Velocidade")]
        public float velocidade = 6f;
        public float multiplicadorCorrida = 3.5f;
        public float sensibilidade = 0.12f;

        [Header("Lutas")]
        [Tooltip("Montado sozinho ao iniciar. Ver o painel na tela.")]
        public int lutaAtual = -1;

        /// <summary>Um atacante e o parceiro que reage a ele.</summary>
        class Luta
        {
            public Transform atacante, parceiro;
            public string rotulo;
        }

        /// <summary>Um agrupamento da cena, pra saltar de area em area.</summary>
        class Area
        {
            public string nome;
            public Bounds caixa;
            public bool temAlgo;
        }

        readonly List<Luta> lutas = new List<Luta>();
        readonly List<Area> areas = new List<Area>();
        float giroX, giroY;
        string ultimoSalto = "";

        void Start()
        {
            var e = transform.eulerAngles;
            giroX = e.y;
            giroY = e.x;
            Levantar();
        }

        // ---------------------------------------------------------------- levantamento

        /// <summary>
        /// Nome do controlador que REAGE a este. Os dois pacotes nomeiam diferente:
        ///   Full Mount:  Paired_..._Att          -> Paired_..._Vic
        ///   Lanca:       ..._Attack1_IP          -> ..._Attack1_React_IP
        ///                ..._Attack1_Stage1_RM   -> ..._Attack1_Stage1_React_RM
        /// Achar o parceiro pelo NOME e' o unico jeito confiavel: com centenas de bonecos
        /// na mesma cena, "o mais perto" cai no boneco da luta vizinha.
        /// </summary>
        static string NomeDoParceiro(string ctrl)
        {
            if (ctrl.EndsWith("_Att")) return ctrl.Substring(0, ctrl.Length - 4) + "_Vic";
            if (ctrl.EndsWith("_IP") || ctrl.EndsWith("_RM"))
            {
                if (ctrl.Contains("_React")) return null;              // ja' e' a reacao
                return ctrl.Substring(0, ctrl.Length - 3) + "_React" + ctrl.Substring(ctrl.Length - 3);
            }
            return null;
        }

        void Levantar()
        {
            lutas.Clear();
            areas.Clear();

            var todos = FindObjectsByType<Animator>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            // indexar por nome de controlador
            var porCtrl = new Dictionary<string, Transform>();
            foreach (var a in todos)
            {
                if (a.runtimeAnimatorController == null) continue;
                var n = a.runtimeAnimatorController.name;
                if (!porCtrl.ContainsKey(n)) porCtrl[n] = a.transform;
            }

            foreach (var a in todos)
            {
                if (a.runtimeAnimatorController == null) continue;
                var n = a.runtimeAnimatorController.name;
                var alvo = NomeDoParceiro(n);
                if (alvo == null || !porCtrl.ContainsKey(alvo)) continue;

                lutas.Add(new Luta {
                    atacante = a.transform,
                    parceiro = porCtrl[alvo],
                    rotulo = n.Replace("Paired_FullMount_", "").Replace("A_SpearCombatAnimationV2_", "")
                });
            }
            lutas.Sort((x, y) => string.CompareOrdinal(x.rotulo, y.rotulo));

            // areas: uma por raiz conhecida da cena
            Juntar("1 finalizacoes", "Paired_FullMount_");
            Juntar("2 bancada", "BANCADA");
            Juntar("3 lanca no lugar", "LANCA — DEMO (parado");
            Juntar("4 lanca andando", "LANCA — DEMO (com desl");
            Juntar("5 orcs", "KEVIN — DEMO (orcs");
            Juntar("6 homem-fera", "KEVIN — DEMO (RESERV");
        }

        void Juntar(string nome, string prefixo)
        {
            var area = new Area { nome = nome };
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (t.parent != null) continue;                        // so' raizes
                if (!t.name.Trim().StartsWith(prefixo)) continue;
                foreach (var r in t.GetComponentsInChildren<Renderer>(false))
                {
                    if (!area.temAlgo) { area.caixa = r.bounds; area.temAlgo = true; }
                    else area.caixa.Encapsulate(r.bounds);
                }
            }
            if (area.temAlgo) areas.Add(area);
        }

        // ---------------------------------------------------------------- por quadro

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            Vector2 andar; float subir; bool correndo, olhando; Vector2 mouse; float roda;
            bool proximo, anterior, reenquadrar; int area;
            LerEntrada(out andar, out subir, out correndo, out olhando, out mouse, out roda,
                       out proximo, out anterior, out reenquadrar, out area);

            if (Mathf.Abs(roda) > 0.01f)
                velocidade = Mathf.Clamp(velocidade * (1f + roda * 0.12f), 0.5f, 80f);

            if (olhando)
            {
                giroX += mouse.x * sensibilidade;
                giroY = Mathf.Clamp(giroY - mouse.y * sensibilidade, -89f, 89f);
                transform.rotation = Quaternion.Euler(giroY, giroX, 0f);
            }

            float v = velocidade * (correndo ? multiplicadorCorrida : 1f);
            var passo = transform.right * andar.x + transform.forward * andar.y + Vector3.up * subir;
            transform.position += passo * v * dt;

            if (area > 0) SaltarPara(area - 1);

            if (proximo || anterior)
            {
                if (lutas.Count == 0) Levantar();
                if (lutas.Count > 0)
                {
                    lutaAtual += anterior ? -1 : 1;
                    if (lutaAtual < 0) lutaAtual = lutas.Count - 1;
                    if (lutaAtual >= lutas.Count) lutaAtual = 0;
                    Enquadrar(lutas[lutaAtual]);
                }
            }
            else if (reenquadrar && lutaAtual >= 0 && lutaAtual < lutas.Count)
                Enquadrar(lutas[lutaAtual]);
        }

        void SaltarPara(int i)
        {
            if (i < 0 || i >= areas.Count) return;
            var a = areas[i];
            ultimoSalto = a.nome;

            // olhar de cima e de lado, longe o bastante pra pegar a area toda
            float precisa = Mathf.Max(a.caixa.size.x, a.caixa.size.z, 8f);
            transform.position = a.caixa.center + new Vector3(0f, precisa * 0.55f, -precisa * 0.75f);
            transform.LookAt(a.caixa.center);
            var e = transform.eulerAngles;
            giroX = e.y; giroY = e.x > 180f ? e.x - 360f : e.x;
            velocidade = Mathf.Clamp(precisa * 0.15f, 6f, 40f);
        }

        void Enquadrar(Luta luta)
        {
            if (luta == null || luta.atacante == null) return;

            var caixa = new Bounds(luta.atacante.position + Vector3.up, Vector3.one * 2f);
            bool primeiro = true;
            foreach (var alvo in new[] { luta.atacante, luta.parceiro })
            {
                if (alvo == null) continue;
                foreach (var r in alvo.GetComponentsInChildren<Renderer>(false))
                {
                    if (primeiro) { caixa = r.bounds; primeiro = false; }
                    else caixa.Encapsulate(r.bounds);
                }
            }

            var cam = GetComponent<Camera>();
            float fov = cam != null ? cam.fieldOfView : 50f;
            float aspecto = cam != null ? cam.aspect : 1.7f;
            float precisa = Mathf.Max(caixa.size.y, caixa.size.x / aspecto, 1.8f);
            float dist = (precisa * 0.5f) / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * 1.7f;

            // olhar de lado, que e' o angulo do jogo 2.5D
            var deOnde = new Vector3(0f, caixa.size.y * 0.45f, -dist);
            transform.position = caixa.center + deOnde;
            transform.LookAt(caixa.center);
            var e = transform.eulerAngles;
            giroX = e.y; giroY = e.x > 180f ? e.x - 360f : e.x;

            Debug.Log("[CameraLivre] " + luta.rotulo + "   (" + (lutaAtual + 1) + "/" + lutas.Count + ")");
        }

        // ---------------------------------------------------------------- entrada

        void LerEntrada(out Vector2 andar, out float subir, out bool correndo, out bool olhando,
                        out Vector2 mouse, out float roda, out bool proximo, out bool anterior,
                        out bool reenquadrar, out int area)
        {
            andar = Vector2.zero; subir = 0f; correndo = false; olhando = false;
            mouse = Vector2.zero; roda = 0f; proximo = false; anterior = false;
            reenquadrar = false; area = 0;

#if ENABLE_INPUT_SYSTEM
            var tec = Keyboard.current;
            var rat = Mouse.current;
            if (tec != null)
            {
                if (tec.wKey.isPressed) andar.y += 1f;
                if (tec.sKey.isPressed) andar.y -= 1f;
                if (tec.dKey.isPressed) andar.x += 1f;
                if (tec.aKey.isPressed) andar.x -= 1f;
                if (tec.eKey.isPressed) subir += 1f;
                if (tec.qKey.isPressed) subir -= 1f;
                correndo = tec.leftShiftKey.isPressed;
                if (tec.tabKey.wasPressedThisFrame)
                {
                    if (correndo) anterior = true; else proximo = true;
                }
                reenquadrar = tec.fKey.wasPressedThisFrame;
                if (tec.digit1Key.wasPressedThisFrame) area = 1;
                if (tec.digit2Key.wasPressedThisFrame) area = 2;
                if (tec.digit3Key.wasPressedThisFrame) area = 3;
                if (tec.digit4Key.wasPressedThisFrame) area = 4;
                if (tec.digit5Key.wasPressedThisFrame) area = 5;
                if (tec.digit6Key.wasPressedThisFrame) area = 6;
            }
            if (rat != null)
            {
                olhando = rat.rightButton.isPressed;
                mouse = rat.delta.ReadValue();
                roda = rat.scroll.ReadValue().y * 0.01f;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKey(KeyCode.W)) andar.y += 1f;
            if (Input.GetKey(KeyCode.S)) andar.y -= 1f;
            if (Input.GetKey(KeyCode.D)) andar.x += 1f;
            if (Input.GetKey(KeyCode.A)) andar.x -= 1f;
            if (Input.GetKey(KeyCode.E)) subir += 1f;
            if (Input.GetKey(KeyCode.Q)) subir -= 1f;
            correndo = Input.GetKey(KeyCode.LeftShift);
            if (Input.GetKeyDown(KeyCode.Tab)) { if (correndo) anterior = true; else proximo = true; }
            reenquadrar = Input.GetKeyDown(KeyCode.F);
            if (Input.GetKeyDown(KeyCode.Alpha1)) area = 1;
            if (Input.GetKeyDown(KeyCode.Alpha2)) area = 2;
            if (Input.GetKeyDown(KeyCode.Alpha3)) area = 3;
            if (Input.GetKeyDown(KeyCode.Alpha4)) area = 4;
            if (Input.GetKeyDown(KeyCode.Alpha5)) area = 5;
            if (Input.GetKeyDown(KeyCode.Alpha6)) area = 6;
            olhando = Input.GetMouseButton(1);
            mouse = new Vector2(Input.GetAxis("Mouse X") * 12f, Input.GetAxis("Mouse Y") * 12f);
            roda = Input.mouseScrollDelta.y;
#endif
            if (andar.sqrMagnitude > 1f) andar.Normalize();
        }

        void OnGUI()
        {
            var estilo = new GUIStyle(GUI.skin.label);
            estilo.fontSize = 13;
            estilo.normal.textColor = Color.white;
            GUI.Box(new Rect(10, 10, 620, 132), GUIContent.none);

            var listaAreas = "";
            for (int i = 0; i < areas.Count; i++)
                listaAreas += (i > 0 ? "  " : "") + areas[i].nome;

            GUI.Label(new Rect(20, 16, 610, 120),
                "WASD  andar     Q / E  descer / subir     Shift  correr\n" +
                "botao direito do mouse  olhar em volta      roda  velocidade (" + velocidade.ToString("F1") + ")\n" +
                "TAB  proxima luta     SHIFT+TAB  anterior     F  reenquadrar\n" +
                listaAreas + (ultimoSalto != "" ? "   → " + ultimoSalto : "") + "\n" +
                (lutaAtual >= 0 && lutaAtual < lutas.Count
                    ? "→ " + lutas[lutaAtual].rotulo + "   (" + (lutaAtual + 1) + "/" + lutas.Count + ")"
                    : "→ aperte TAB pra ir pra primeira luta   (" + lutas.Count + " no total)"),
                estilo);
        }
    }
}
