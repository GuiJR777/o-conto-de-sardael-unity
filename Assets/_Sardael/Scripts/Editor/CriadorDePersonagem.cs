using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Sardael.EditorTools
{
    /// <summary>
    /// Criador de personagens para o sistema modular da Synty (PolygonFantasyHeroCharacters).
    ///
    /// O prefab ModularCharacters traz 720 malhas ligadas ao mesmo tempo; um personagem e'
    /// "uma peca ligada por slot". Esta janela troca peca por peca com as setas e mostra o
    /// resultado num preview 3D DENTRO da propria janela — nao precisa dar Play nem enxergar
    /// a Scene por tras. A camera pula sozinha pra parte do corpo do slot que voce esta' mexendo.
    ///
    /// CORES: as 720 malhas dividem UM material (FantasyHero.mat). Mexer nele pintaria todo
    /// personagem do projeto de uma vez, entao a janela trabalha sempre numa COPIA e, ao salvar,
    /// grava um material proprio do personagem ao lado do prefab.
    ///
    /// SALVAR: as pecas nao escolhidas sao APAGADAS — sobra o esqueleto + as ~15 malhas usadas.
    /// Junto vai um .json com a receita, que e' o que permite reabrir e editar depois (o prefab
    /// salvo, sozinho, ja' perdeu as outras pecas).
    /// </summary>
    public class CriadorDePersonagem : EditorWindow
    {
        // ------------------------------------------------------------------ caminhos
        const string MODULAR  = "Assets/Synty/PolygonFantasyHeroCharacters/Prefabs/ModularCharacters.prefab";
        const string PRESETS  = "Assets/Synty/PolygonFantasyHeroCharacters/Prefabs/Characters_Presets";
        const string MAT_BASE = "Assets/Synty/PolygonFantasyHeroCharacters/Materials/FantasyHero.mat";
        const string SAIDA    = "Assets/_Sardael/Personagens";

        static readonly string[] PASTAS_ARMAS =
        {
            "Assets/Synty/PolygonFantasyHeroCharacters/Prefabs/Weapons",
            "Assets/Synty/PolygonGoblinWarCamp/Prefabs/Weapons",
        };

        static readonly string[] OSSOS =
        {
            "Hand_R", "Hand_L",
            "Back_Attachment", "Hips_Attachment", "Chest_Attachment", "Head_Attachment",
            "Shoulder_Attachment_R", "Shoulder_Attachment_L",
            "Elbow_Attachment_R", "Elbow_Attachment_L",
            "Knee_Attachment_R", "Knee_Attachment_L",
        };

        static readonly string[] OBRIGATORIOS =
        {
            "Head", "Torso", "Hips",
            "Arm_Upper_Right", "Arm_Upper_Left", "Arm_Lower_Right", "Arm_Lower_Left",
            "Hand_Right", "Hand_Left", "Leg_Right", "Leg_Left",
        };

        // propriedades do shader Synty/POLYGON_CustomCharacters (conferidas no material do pack)
        static readonly string[] COR_PROP =
        {
            "_Color_Skin", "_Color_Hair", "_Color_Stubble", "_Color_Scar", "_Color_Eyes",
            "_Color_Primary", "_Color_Secondary",
            "_Color_Leather_Primary", "_Color_Leather_Secondary",
            "_Color_Metal_Primary", "_Color_Metal_Secondary", "_Color_Metal_Dark",
            "_Color_Body_Art",
        };
        static readonly string[] COR_NOME =
        {
            "Pele", "Cabelo", "Barba por fazer", "Cicatriz", "Olhos",
            "Roupa 1", "Roupa 2",
            "Couro 1", "Couro 2",
            "Metal 1", "Metal 2", "Metal escuro",
            "Tatuagem",
        };
        static readonly string[] NUM_PROP = { "_Body_Art_Amount", "_Metallic", "_Smoothness", "_Emission" };
        static readonly string[] NUM_NOME = { "Tatuagem (quanto)", "Metalico", "Brilho", "Emissao" };

        static readonly string[] ABAS = { "Pecas", "Cores", "Armas" };

        // ------------------------------------------------------------------ estado
        [SerializeField] GameObject alvo;
        [SerializeField] string nome = "Sardael";
        [SerializeField] bool espelhar = true;
        [SerializeField] bool feminino = false;
        [SerializeField] bool seguirSlot = true;
        [SerializeField] int aba = 0;
        [SerializeField] int presetSel = 0;
        [SerializeField] int armaSel = 0;
        [SerializeField] int ossoSel = 0;
        [SerializeField] string ultimoSalvo = "";

        [SerializeField] Color[] cores;
        [SerializeField] float[] numeros;

        [SerializeField] float camGiro = 160f;
        [SerializeField] float camAltura = 12f;
        [SerializeField] float camAlvoY = 0.95f;
        [SerializeField] float camDist = 3.4f;

        readonly List<Slot> slots = new List<Slot>();
        Vector2 rolagem;
        string aviso = "";
        MessageType avisoTipo = MessageType.Info;

        string[] presetsCaminhos, presetsNomes, armasCaminhos, armasNomes;

        Material matEdicao;                 // copia de trabalho, nunca o material do pack
        PreviewRenderUtility pru;
        readonly List<Peca> pecas = new List<Peca>();
        bool precisaAssar = true;
        bool arrastando;

        struct Peca
        {
            public Mesh malha;
            public Material material;
            public Matrix4x4 matriz;
            public int sub;
            public bool descartavel;
        }

        class Slot
        {
            public string grupo, bruto, rotulo, chave;
            public readonly List<GameObject> opcoes = new List<GameObject>();
            public string[] rotulos;
            public int escolha = -1;
            public int par = -1;
            public bool obrigatorio;
            public float focoY = 0.95f, focoDist = 3.4f;
        }

        [System.Serializable]
        class ReceitaArma
        {
            public string prefab, osso;
            public Vector3 pos, rot;
            public Vector3 esc = Vector3.one;
        }

        [System.Serializable]
        class Receita
        {
            public string nome, genero;
            public List<string> pecas = new List<string>();
            public List<ReceitaArma> armas = new List<ReceitaArma>();
            public List<Color> cores = new List<Color>();
            public List<float> numeros = new List<float>();
        }

        // ------------------------------------------------------------------ ciclo de vida
        [MenuItem("Sardael/Criador de Personagem", false, 0)]
        static void Abrir()
        {
            var j = GetWindow<CriadorDePersonagem>("Criador");
            j.minSize = new Vector2(440f, 700f);
            j.Show();
        }

        void OnEnable()
        {
            CarregarListas();
            if (cores == null || cores.Length != COR_PROP.Length) CoresPadrao();
            precisaAssar = true;
        }

        void OnDisable()
        {
            LimparPecas();
            if (pru != null) { pru.Cleanup(); pru = null; }
            if (matEdicao != null) { DestroyImmediate(matEdicao); matEdicao = null; }
        }

        void CarregarListas()
        {
            var lc = new List<string> { "" };
            var ln = new List<string> { "— escolher preset —" };
            if (AssetDatabase.IsValidFolder(PRESETS))
                for (int i = 1; i <= 200; i++)
                {
                    var c = PRESETS + "/Chr_FantasyHero_Preset_" + i + ".prefab";
                    if (File.Exists(c)) { lc.Add(c); ln.Add("Preset " + i); }
                }
            presetsCaminhos = lc.ToArray();
            presetsNomes = ln.ToArray();

            var ac = new List<string> { "" };
            var an = new List<string> { "— escolher arma —" };
            for (int p = 0; p < PASTAS_ARMAS.Length; p++)
            {
                if (!AssetDatabase.IsValidFolder(PASTAS_ARMAS[p])) continue;
                string grupo = p == 0 ? "Heroi" : "Orc";
                var caminhos = new List<string>();
                foreach (var g in AssetDatabase.FindAssets("t:GameObject", new[] { PASTAS_ARMAS[p] }))
                    caminhos.Add(AssetDatabase.GUIDToAssetPath(g));
                caminhos.Sort();
                foreach (var c in caminhos) { ac.Add(c); an.Add(grupo + "/" + Path.GetFileNameWithoutExtension(c)); }
            }
            armasCaminhos = ac.ToArray();
            armasNomes = an.ToArray();
        }

        // ------------------------------------------------------------------ GUI
        void OnGUI()
        {
            if (alvo != null && PrecisaReconstruir()) { ConstruirSlots(); precisaAssar = true; }
            if (alvo != null && matEdicao == null) { CriarMaterial(); AplicarMaterial(); precisaAssar = true; }

            Cabecalho();

            if (alvo == null)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox(
                    "Nenhum personagem em edicao.\n\n" +
                    "Clique em NOVO PERSONAGEM. O boneco aparece aqui embaixo, girando — " +
                    "voce nao precisa dar Play nem enxergar a Scene atras desta janela.",
                    MessageType.Info);
                return;
            }

            Preview();
            EditorGUILayout.Space(2);
            BarraCamera();
            EditorGUILayout.Space(2);
            BarraGeral();
            EditorGUILayout.Space(4);

            aba = GUILayout.Toolbar(aba, ABAS, GUILayout.Height(22));
            EditorGUILayout.Space(2);

            rolagem = EditorGUILayout.BeginScrollView(rolagem);
            if (aba == 0) AbaPecas();
            else if (aba == 1) AbaCores();
            else AbaArmas();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            Salvamento();
        }

        void Cabecalho()
        {
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("NOVO PERSONAGEM", GUILayout.Height(26))) NovoPersonagem();
                if (GUILayout.Button("Adotar selecao", GUILayout.Height(26), GUILayout.Width(108))) AdotarSelecao();
            }
            if (!string.IsNullOrEmpty(aviso)) EditorGUILayout.HelpBox(aviso, avisoTipo);
        }

        // ------------------------------------------------------------------ preview 3D
        void Preview()
        {
            var r = GUILayoutUtility.GetRect(10f, 10000f, 250f, 250f);
            InteragirPreview(r);
            if (Event.current.type != EventType.Repaint) return;

            if (pru == null) pru = new PreviewRenderUtility();
            if (precisaAssar) Assar();

            var giro = Quaternion.Euler(camAltura, camGiro, 0f);
            var pivo = new Vector3(0f, camAlvoY, 0f);

            pru.camera.clearFlags = CameraClearFlags.SolidColor;
            pru.camera.backgroundColor = EditorGUIUtility.isProSkin
                ? new Color(0.17f, 0.17f, 0.19f, 1f)
                : new Color(0.62f, 0.63f, 0.66f, 1f);
            pru.camera.nearClipPlane = 0.02f;
            pru.camera.farClipPlane = 60f;
            pru.camera.fieldOfView = 32f;
            pru.camera.transform.position = pivo + giro * new Vector3(0f, 0f, -camDist);
            pru.camera.transform.rotation = giro;

            pru.lights[0].intensity = 1.25f;
            pru.lights[0].transform.rotation = Quaternion.Euler(35f, camGiro + 40f, 0f);
            pru.lights[1].intensity = 0.75f;
            pru.lights[1].transform.rotation = Quaternion.Euler(-10f, camGiro - 110f, 0f);
            pru.ambientColor = new Color(0.40f, 0.40f, 0.46f, 1f);

            pru.BeginPreview(r, GUIStyle.none);
            for (int i = 0; i < pecas.Count; i++)
            {
                var p = pecas[i];
                if (p.malha == null || p.material == null) continue;
                pru.DrawMesh(p.malha, p.matriz, p.material, p.sub);
            }
            pru.Render(true);          // true = deixa a URP renderizar. Sem isso o preview sai vazio.
            GUI.DrawTexture(r, pru.EndPreview(), ScaleMode.StretchToFill, false);

            var antes = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.45f);
            GUI.Label(new Rect(r.x + 6f, r.yMax - 18f, r.width - 12f, 16f),
                "arraste pra girar · roda do mouse pra aproximar", EditorStyles.miniLabel);
            GUI.color = antes;
        }

        void InteragirPreview(Rect r)
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown && r.Contains(e.mousePosition)) { arrastando = true; e.Use(); }
            else if (e.type == EventType.MouseDrag && arrastando)
            {
                camGiro -= e.delta.x * 0.7f;
                camAltura = Mathf.Clamp(camAltura + e.delta.y * 0.5f, -35f, 70f);
                Repaint(); e.Use();
            }
            else if (e.type == EventType.MouseUp && arrastando) { arrastando = false; e.Use(); }
            else if (e.type == EventType.ScrollWheel && r.Contains(e.mousePosition))
            {
                camDist = Mathf.Clamp(camDist * (1f + e.delta.y * 0.06f), 0.35f, 9f);
                Repaint(); e.Use();
            }
        }

        void LimparPecas()
        {
            for (int i = 0; i < pecas.Count; i++)
                if (pecas[i].descartavel && pecas[i].malha != null) DestroyImmediate(pecas[i].malha);
            pecas.Clear();
        }

        void Assar()
        {
            LimparPecas();
            precisaAssar = false;
            if (alvo == null) return;
            var paraRaiz = alvo.transform.worldToLocalMatrix;

            // corpo: BakeMesh entrega a malha ja' no espaco do esqueleto
            foreach (var smr in alvo.GetComponentsInChildren<SkinnedMeshRenderer>(false))
            {
                if (smr.sharedMesh == null) continue;
                var assada = new Mesh();
                smr.BakeMesh(assada);
                var mats = smr.sharedMaterials;
                for (int s = 0; s < assada.subMeshCount; s++)
                {
                    var mat = s < mats.Length ? mats[s] : (mats.Length > 0 ? mats[0] : null);
                    if (mat == null) continue;
                    pecas.Add(new Peca { malha = assada, material = mat, matriz = Matrix4x4.identity, sub = s, descartavel = s == 0 });
                }
            }

            // armas: MeshRenderer comum, posicionado pelo osso
            foreach (var mf in alvo.GetComponentsInChildren<MeshFilter>(false))
            {
                if (mf.sharedMesh == null) continue;
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || !mr.enabled) continue;
                var mtx = paraRaiz * mf.transform.localToWorldMatrix;
                var mats = mr.sharedMaterials;
                for (int s = 0; s < mf.sharedMesh.subMeshCount; s++)
                {
                    var mat = s < mats.Length ? mats[s] : (mats.Length > 0 ? mats[0] : null);
                    if (mat == null) continue;
                    pecas.Add(new Peca { malha = mf.sharedMesh, material = mat, matriz = mtx, sub = s, descartavel = false });
                }
            }
        }

        // ------------------------------------------------------------------ barras
        void BarraCamera()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Corpo inteiro", EditorStyles.miniButtonLeft)) Enquadrar(0.95f, 3.4f);
                if (GUILayout.Button("Cabeca", EditorStyles.miniButtonMid)) Enquadrar(1.66f, 0.95f);
                if (GUILayout.Button("Torso", EditorStyles.miniButtonMid)) Enquadrar(1.25f, 1.9f);
                if (GUILayout.Button("Pernas", EditorStyles.miniButtonMid)) Enquadrar(0.55f, 1.9f);
                if (GUILayout.Button("Frente", EditorStyles.miniButtonMid)) { camGiro = 180f; camAltura = 6f; Repaint(); }
                if (GUILayout.Button("Costas", EditorStyles.miniButtonRight)) { camGiro = 0f; camAltura = 6f; Repaint(); }
            }
        }

        void BarraGeral()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginChangeCheck();
                bool f = GUILayout.Toggle(feminino, feminino ? "  Feminino" : "  Masculino", EditorStyles.miniButton, GUILayout.Width(84));
                if (EditorGUI.EndChangeCheck()) TrocarGenero(f);

                espelhar = GUILayout.Toggle(espelhar, new GUIContent("  Espelhar L/R",
                    "Trocou um lado (braco, mao, perna, ombro...), o outro acompanha."),
                    EditorStyles.miniButton, GUILayout.Width(94));

                seguirSlot = GUILayout.Toggle(seguirSlot, new GUIContent("  Seguir",
                    "A camera pula sozinha pra parte do corpo do slot que voce esta' mexendo."),
                    EditorStyles.miniButton, GUILayout.Width(60));

                if (GUILayout.Button("Aleatorio", EditorStyles.miniButton)) Aleatorio();
                if (GUILayout.Button("Limpar", EditorStyles.miniButton)) { ApagarTudo(); Avisar("Tudo desligado.", MessageType.Info); }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                presetSel = EditorGUILayout.Popup(presetSel, presetsNomes);
                if (EditorGUI.EndChangeCheck() && presetSel > 0) CarregarPreset(presetsCaminhos[presetSel]);
                GUILayout.Label(new GUIContent("  ponto de partida",
                    "Carrega um dos 120 personagens prontos da Synty pra voce alterar por cima."),
                    EditorStyles.miniLabel, GUILayout.Width(104));
            }
        }

        void Enquadrar(float y, float dist) { camAlvoY = y; camDist = dist; Repaint(); }

        // ------------------------------------------------------------------ aba: pecas
        void AbaPecas()
        {
            string grupoGenero = feminino ? "Female_Parts" : "Male_Parts";
            string grupoAtual = "";
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (s.grupo != "All_Gender_Parts" && s.grupo != grupoGenero) continue;

                if (s.grupo != grupoAtual)
                {
                    grupoAtual = s.grupo;
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField(
                        grupoAtual == "All_Gender_Parts" ? "CABELO E ENCAIXES" : "CORPO", EditorStyles.boldLabel);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    var cor = GUI.color;
                    if (s.obrigatorio && s.escolha < 0) GUI.color = new Color(1f, 0.55f, 0.55f);
                    GUILayout.Label(s.rotulo, GUILayout.Width(142));
                    GUI.color = cor;

                    if (GUILayout.Button("◀", EditorStyles.miniButtonLeft, GUILayout.Width(24)))
                        Definir(s, Ciclar(s, -1), true);

                    EditorGUI.BeginChangeCheck();
                    int novo = EditorGUILayout.Popup(s.escolha + 1, s.rotulos) - 1;
                    if (EditorGUI.EndChangeCheck()) Definir(s, novo, true);

                    if (GUILayout.Button("▶", EditorStyles.miniButtonRight, GUILayout.Width(24)))
                        Definir(s, Ciclar(s, +1), true);

                    GUILayout.Label((s.escolha < 0 ? "-" : (s.escolha + 1).ToString()) + "/" + s.opcoes.Count,
                        EditorStyles.miniLabel, GUILayout.Width(44));
                }
            }
        }

        // ------------------------------------------------------------------ aba: cores
        void AbaCores()
        {
            EditorGUILayout.HelpBox(
                "As cores sao do personagem, nao do pacote: a janela trabalha numa copia do " +
                "FantasyHero.mat e grava um material proprio ao salvar. Os outros personagens nao mudam.",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Cores aleatorias", EditorStyles.miniButtonLeft)) CoresAleatorias();
                if (GUILayout.Button("Voltar ao padrao", EditorStyles.miniButtonRight)) { CoresPadrao(); AplicarCores(); }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("CORPO", EditorStyles.boldLabel);
            for (int i = 0; i < 5; i++) CampoCor(i);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("EQUIPAMENTO", EditorStyles.boldLabel);
            for (int i = 5; i < COR_PROP.Length; i++) CampoCor(i);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("ACABAMENTO", EditorStyles.boldLabel);
            for (int i = 0; i < NUM_PROP.Length; i++)
            {
                EditorGUI.BeginChangeCheck();
                numeros[i] = EditorGUILayout.Slider(NUM_NOME[i], numeros[i], 0f, 1f);
                if (EditorGUI.EndChangeCheck()) AplicarCores();
            }
        }

        void CampoCor(int i)
        {
            EditorGUI.BeginChangeCheck();
            cores[i] = EditorGUILayout.ColorField(COR_NOME[i], cores[i]);
            if (EditorGUI.EndChangeCheck()) AplicarCores();
        }

        void CoresPadrao()
        {
            var bas = AssetDatabase.LoadAssetAtPath<Material>(MAT_BASE);
            cores = new Color[COR_PROP.Length];
            numeros = new float[NUM_PROP.Length];
            for (int i = 0; i < COR_PROP.Length; i++)
                cores[i] = bas != null && bas.HasProperty(COR_PROP[i]) ? bas.GetColor(COR_PROP[i]) : Color.gray;
            for (int i = 0; i < NUM_PROP.Length; i++)
                numeros[i] = bas != null && bas.HasProperty(NUM_PROP[i]) ? bas.GetFloat(NUM_PROP[i]) : 0f;
        }

        void CoresAleatorias()
        {
            // pele e cabelo em faixas plausiveis; equipamento livre mas dessaturado (estilo Synty)
            cores[0] = Color.Lerp(new Color(0.95f, 0.80f, 0.66f), new Color(0.35f, 0.22f, 0.14f), Random.value);
            cores[1] = Random.value < 0.15f
                ? Color.Lerp(new Color(0.85f, 0.85f, 0.85f), new Color(0.6f, 0.6f, 0.62f), Random.value)
                : Color.HSVToRGB(Random.Range(0.02f, 0.12f), Random.Range(0.25f, 0.75f), Random.Range(0.12f, 0.55f));
            cores[2] = Color.Lerp(cores[1], cores[0], 0.5f);
            cores[3] = Color.Lerp(cores[0], Color.white, 0.25f);
            for (int i = 5; i < COR_PROP.Length; i++)
                cores[i] = Color.HSVToRGB(Random.value, Random.Range(0.10f, 0.45f), Random.Range(0.25f, 0.70f));
            AplicarCores();
        }

        void CriarMaterial()
        {
            var bas = AssetDatabase.LoadAssetAtPath<Material>(MAT_BASE);
            if (bas == null) { Avisar("Nao achei " + MAT_BASE, MessageType.Error); return; }
            matEdicao = new Material(bas) { name = "FantasyHero (em edicao)", hideFlags = HideFlags.DontSave };
            AplicarCores();
        }

        void AplicarCores()
        {
            if (matEdicao == null) return;
            for (int i = 0; i < COR_PROP.Length; i++)
                if (matEdicao.HasProperty(COR_PROP[i])) matEdicao.SetColor(COR_PROP[i], cores[i]);
            for (int i = 0; i < NUM_PROP.Length; i++)
                if (matEdicao.HasProperty(NUM_PROP[i])) matEdicao.SetFloat(NUM_PROP[i], numeros[i]);
            Repaint();
            SceneView.RepaintAll();
        }

        /// <summary>Poe a copia de trabalho em todas as malhas, pra nunca pintar o material do pack.</summary>
        void AplicarMaterial()
        {
            if (alvo == null || matEdicao == null) return;
            foreach (var smr in alvo.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                smr.sharedMaterial = matEdicao;
        }

        // ------------------------------------------------------------------ aba: armas
        void AbaArmas()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                armaSel = EditorGUILayout.Popup(armaSel, armasNomes);
                ossoSel = EditorGUILayout.Popup(ossoSel, OSSOS, GUILayout.Width(140));
                if (GUILayout.Button("Encaixar", GUILayout.Width(70))) Encaixar();
            }

            EditorGUILayout.Space(4);
            var presas = ListarArmas();
            if (presas.Count == 0)
                EditorGUILayout.LabelField("   (nenhuma arma encaixada ainda)", EditorStyles.miniLabel);

            for (int i = 0; i < presas.Count; i++)
            {
                var t = presas[i];
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(t.name + "  →  " + t.parent.name, EditorStyles.boldLabel);
                        if (GUILayout.Button("ajustar na Scene", EditorStyles.miniButton, GUILayout.Width(106)))
                        { Selection.activeGameObject = t.gameObject; FocarNaScene(); }
                        if (GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(20)))
                        { Undo.DestroyObjectImmediate(t.gameObject); precisaAssar = true; GUIUtility.ExitGUI(); }
                    }
                    EditorGUI.BeginChangeCheck();
                    var pos = EditorGUILayout.Vector3Field("posicao", t.localPosition);
                    var rot = EditorGUILayout.Vector3Field("rotacao", t.localEulerAngles);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(t, "Ajustar arma");
                        t.localPosition = pos;
                        t.localEulerAngles = rot;
                        precisaAssar = true;
                        Sujar();
                    }
                }
            }
        }

        // ------------------------------------------------------------------ salvar
        void Salvamento()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Nome", GUILayout.Width(38));
                    nome = EditorGUILayout.TextField(nome);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("SALVAR PERSONAGEM", GUILayout.Height(28))) Salvar();
                    if (GUILayout.Button("Reabrir...", GUILayout.Height(28), GUILayout.Width(86))) Reabrir();
                }
                if (!string.IsNullOrEmpty(ultimoSalvo))
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(ultimoSalvo, EditorStyles.miniLabel);
                        if (GUILayout.Button("copiar", EditorStyles.miniButton, GUILayout.Width(52)))
                            EditorGUIUtility.systemCopyBuffer = ultimoSalvo;
                    }
            }
        }

        void Salvar()
        {
            if (alvo == null) { Avisar("Nao ha' personagem em edicao.", MessageType.Warning); return; }
            string limpo = nome.Trim();
            if (string.IsNullOrEmpty(limpo)) { Avisar("De' um nome ao personagem.", MessageType.Warning); return; }

            var faltando = new List<string>();
            foreach (var s in slots)
                if (s.obrigatorio && Visivel(s) && s.escolha < 0) faltando.Add(s.rotulo);
            if (faltando.Count > 0 &&
                !EditorUtility.DisplayDialog("Faltam pecas",
                    "Esses slots estao vazios:\n\n  " + string.Join("\n  ", faltando.ToArray()) +
                    "\n\nSalvar assim mesmo?", "Salvar assim", "Voltar"))
                return;

            GarantirPasta(SAIDA);
            string caminhoPrefab = AssetDatabase.GenerateUniqueAssetPath(SAIDA + "/" + limpo + ".prefab");
            string baseSemExt = Path.ChangeExtension(caminhoPrefab, null);

            // material proprio do personagem, pra nunca mexer no material do pacote
            var matPersonagem = new Material(matEdicao) { name = Path.GetFileName(baseSemExt) };
            AssetDatabase.CreateAsset(matPersonagem, baseSemExt + ".mat");

            int mantidas, apagadas;
            var prefab = SalvarLimpo(alvo, limpo, caminhoPrefab, matPersonagem, out mantidas, out apagadas);
            if (prefab == null) { Avisar("Unity recusou salvar o prefab.", MessageType.Error); return; }

            EscreverReceita(baseSemExt + ".json", limpo);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            ultimoSalvo = caminhoPrefab;
            Avisar("SALVO: " + caminhoPrefab + "\n" + mantidas + " malhas mantidas, " + apagadas +
                   " apagadas. Material e receita .json ficaram do lado — e' a receita que reabre pra editar.",
                   MessageType.Info);
            EditorGUIUtility.PingObject(prefab);
        }

        /// <summary>
        /// Duplica o personagem, APAGA toda peca desligada, poda as pastas vazias, troca o
        /// material pelo do personagem e grava o prefab. Esqueleto e armas ficam.
        /// </summary>
        public static GameObject SalvarLimpo(GameObject fonte, string nomeFinal, string caminhoPrefab,
                                             Material matPersonagem, out int mantidas, out int apagadas)
        {
            mantidas = 0; apagadas = 0;
            if (fonte == null || string.IsNullOrEmpty(caminhoPrefab)) return null;

            var copia = Instantiate(fonte);
            copia.name = nomeFinal;
            copia.transform.position = Vector3.zero;
            copia.transform.rotation = Quaternion.identity;
            copia.transform.localScale = Vector3.one;

            // O ModularCharacters da Synty carrega o CharacterRandomizer, que em Play SORTEIA
            // um personagem: desliga tudo e religa pecas ao acaso. Num personagem salvo isso e'
            // desastre — as pecas que ele tentaria religar foram apagadas, entao partes do
            // corpo simplesmente somem, e diferente a cada execucao. Fora com os scripts do
            // pacote de demonstracao.
            foreach (var mb in copia.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                var tipo = mb.GetType();
                if (tipo.Namespace == "PsychoticLab" || tipo.Name == "CharacterRandomizer")
                    DestroyImmediate(mb, true);
            }

            foreach (var smr in copia.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr == null) continue;
                if (LigadoAteRaiz(smr.transform, copia.transform))
                {
                    if (matPersonagem != null) smr.sharedMaterial = matPersonagem;
                    // Os bounds que a Synty grava valem pra pose de repouso. Em animacao de
                    // chao (queda, montada, finalizacao) o corpo sai dessa caixa e a peca e'
                    // descartada pelo frustum — o rosto some e volta conforme o angulo.
                    // Recalcular por frame custa pouco em ~15 renderers e mata o problema.
                    smr.updateWhenOffscreen = true;
                    mantidas++;
                    continue;
                }
                DestroyImmediate(smr.gameObject);
                apagadas++;
            }

            var mc = copia.transform.Find("Modular_Characters");
            if (mc != null)
                for (int i = mc.childCount - 1; i >= 0; i--) PodarVazios(mc.GetChild(i));

            var prefab = PrefabUtility.SaveAsPrefabAsset(copia, caminhoPrefab);
            DestroyImmediate(copia);
            return prefab;
        }

        static void PodarVazios(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) PodarVazios(t.GetChild(i));
            if (t.GetComponentsInChildren<Renderer>(true).Length == 0) DestroyImmediate(t.gameObject);
        }

        void EscreverReceita(string caminhoJson, string nomePers)
        {
            var r = new Receita { nome = nomePers, genero = feminino ? "Female" : "Male" };
            foreach (var s in slots)
                if (s.escolha >= 0 && s.opcoes[s.escolha] != null) r.pecas.Add(s.opcoes[s.escolha].name);
            foreach (var c in cores) r.cores.Add(c);
            foreach (var n in numeros) r.numeros.Add(n);

            foreach (var t in ListarArmas())
            {
                var fonte = PrefabUtility.GetCorrespondingObjectFromOriginalSource(t.gameObject);
                r.armas.Add(new ReceitaArma
                {
                    prefab = fonte != null ? AssetDatabase.GetAssetPath(fonte) : "",
                    osso = t.parent.name,
                    pos = t.localPosition,
                    rot = t.localEulerAngles,
                    esc = t.localScale,
                });
            }
            File.WriteAllText(caminhoJson, JsonUtility.ToJson(r, true), new UTF8Encoding(false));
        }

        void Reabrir()
        {
            GarantirPasta(SAIDA);
            string abs = EditorUtility.OpenFilePanel("Abrir receita de personagem", SAIDA, "json");
            if (string.IsNullOrEmpty(abs)) return;

            Receita r;
            try { r = JsonUtility.FromJson<Receita>(File.ReadAllText(abs)); }
            catch { Avisar("Nao consegui ler essa receita.", MessageType.Error); return; }
            if (r == null || r.pecas == null) { Avisar("Receita vazia ou invalida.", MessageType.Error); return; }

            NovoPersonagem();
            feminino = r.genero == "Female";
            AplicarNomes(new HashSet<string>(r.pecas));

            if (r.cores != null && r.cores.Count == COR_PROP.Length) cores = r.cores.ToArray();
            if (r.numeros != null && r.numeros.Count == NUM_PROP.Length) numeros = r.numeros.ToArray();
            AplicarCores();

            foreach (var a in r.armas)
            {
                if (string.IsNullOrEmpty(a.prefab)) continue;
                var pre = AssetDatabase.LoadAssetAtPath<GameObject>(a.prefab);
                var osso = AcharOsso(a.osso);
                if (pre == null || osso == null) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pre, osso);
                go.transform.localPosition = a.pos;
                go.transform.localEulerAngles = a.rot;
                go.transform.localScale = a.esc == Vector3.zero ? EscalaCompensada(osso) : a.esc;
            }

            nome = r.nome;
            alvo.name = "PERSONAGEM_EM_EDICAO (" + r.nome + ")";
            precisaAssar = true;
            Enquadrar(0.95f, 3.4f);
            Avisar("Receita '" + r.nome + "' carregada com " + r.pecas.Count + " pecas.", MessageType.Info);
        }

        // ------------------------------------------------------------------ montagem dos slots
        bool PrecisaReconstruir()
        {
            if (slots.Count == 0) return true;
            if (slots[0].opcoes.Count == 0) return true;
            if (slots[0].opcoes[0] == null) return true;
            return !slots[0].opcoes[0].transform.IsChildOf(alvo.transform);
        }

        void ConstruirSlots()
        {
            slots.Clear();
            if (alvo == null) return;
            var mc = alvo.transform.Find("Modular_Characters");
            if (mc == null) { Avisar("Esse objeto nao e' um ModularCharacters.", MessageType.Error); alvo = null; return; }

            foreach (Transform grupo in mc)
            foreach (Transform pasta in grupo)
            {
                var s = new Slot { grupo = grupo.name, bruto = pasta.name, chave = SemPrefixo(pasta.name) };
                s.rotulo = s.chave.Replace("_", " ");

                foreach (var smr in pasta.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    s.opcoes.Add(smr.gameObject);
                if (s.opcoes.Count == 0) continue;

                s.rotulos = new string[s.opcoes.Count + 1];
                s.rotulos[0] = "— nenhum —";
                for (int i = 0; i < s.opcoes.Count; i++) s.rotulos[i + 1] = s.opcoes[i].name;

                s.escolha = -1;
                for (int i = 0; i < s.opcoes.Count; i++)
                    if (s.opcoes[i].activeSelf) { s.escolha = i; break; }

                foreach (var o in OBRIGATORIOS) if (o == s.chave) { s.obrigatorio = true; break; }
                DefinirFoco(s);
                slots.Add(s);
            }

            for (int i = 0; i < slots.Count; i++)
            {
                var a = slots[i];
                if (a.par >= 0) continue;
                string procurado = a.chave.Contains("Right") ? a.chave.Replace("Right", "Left")
                                 : a.chave.Contains("Left")  ? a.chave.Replace("Left", "Right") : null;
                if (procurado == null) continue;
                for (int j = 0; j < slots.Count; j++)
                {
                    if (i == j || slots[j].grupo != a.grupo || slots[j].chave != procurado) continue;
                    a.par = j; slots[j].par = i; break;
                }
            }
        }

        static void DefinirFoco(Slot s)
        {
            string c = s.chave;
            if (c.Contains("Head") || c.Contains("Hair") || c.Contains("Eyebrow") ||
                c.Contains("Ear") || c.Contains("Extra") || c.Contains("Helmet"))
            { s.focoY = 1.66f; s.focoDist = 0.95f; return; }
            if (c.Contains("Hand") || c.Contains("Arm_Lower") || c.Contains("Elbow"))
            { s.focoY = 1.15f; s.focoDist = 2.2f; return; }
            if (c.Contains("Torso") || c.Contains("Arm_Upper") || c.Contains("Shoulder") ||
                c.Contains("Chest") || c.Contains("Back"))
            { s.focoY = 1.28f; s.focoDist = 1.9f; return; }
            if (c.Contains("Hips") || c.Contains("Leg") || c.Contains("Knee"))
            { s.focoY = 0.55f; s.focoDist = 1.9f; return; }
            s.focoY = 0.95f; s.focoDist = 3.4f;
        }

        static string SemPrefixo(string bruto)
        {
            var p = bruto.Split('_');
            return p.Length > 2 ? string.Join("_", p, 2, p.Length - 2) : bruto;
        }

        // ------------------------------------------------------------------ acoes
        void NovoPersonagem()
        {
            var pre = AssetDatabase.LoadAssetAtPath<GameObject>(MODULAR);
            if (pre == null) { Avisar("Nao achei o prefab em " + MODULAR, MessageType.Error); return; }

            var go = Instantiate(pre);
            go.name = "PERSONAGEM_EM_EDICAO";
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            Undo.RegisterCreatedObjectUndo(go, "Novo personagem");

            alvo = go;
            ConstruirSlots();
            if (matEdicao == null) CriarMaterial();
            AplicarMaterial();
            ApagarTudo();
            Base();
            Enquadrar(0.95f, 3.4f);
            Avisar("Pronto. Use as setas ◀ ▶ — o boneco aqui em cima muda na hora.", MessageType.Info);
        }

        void AdotarSelecao()
        {
            var sel = Selection.activeGameObject;
            if (sel == null) { Avisar("Selecione o personagem na Hierarchy primeiro.", MessageType.Warning); return; }
            if (sel.transform.Find("Modular_Characters") == null)
            { Avisar("Esse objeto nao tem Modular_Characters. Selecione a RAIZ do personagem.", MessageType.Warning); return; }
            alvo = sel;
            ConstruirSlots();
            if (matEdicao == null) CriarMaterial();
            AplicarMaterial();
            precisaAssar = true;
            Avisar("Editando " + sel.name + ".", MessageType.Info);
        }

        void ApagarTudo()
        {
            if (alvo == null) return;
            Undo.RegisterFullObjectHierarchyUndo(alvo, "Limpar personagem");
            foreach (var s in slots)
            {
                foreach (var o in s.opcoes) if (o != null) o.SetActive(false);
                s.escolha = -1;
            }
            precisaAssar = true;
            Sujar();
        }

        void Base()
        {
            foreach (var s in slots)
                if (s.obrigatorio && Visivel(s)) Definir(s, 0, false, false);
        }

        bool Visivel(Slot s)
        {
            return s.grupo == "All_Gender_Parts" || s.grupo == (feminino ? "Female_Parts" : "Male_Parts");
        }

        int Ciclar(Slot s, int passo)
        {
            int n = s.opcoes.Count;
            int i = s.escolha + 1 + passo;
            if (i < 0) i = n;
            if (i > n) i = 0;
            return i - 1;
        }

        void Definir(Slot s, int idx, bool propagar) { Definir(s, idx, propagar, seguirSlot); }

        void Definir(Slot s, int idx, bool propagar, bool focar)
        {
            if (s == null) return;
            idx = Mathf.Clamp(idx, -1, s.opcoes.Count - 1);

            if (s.escolha >= 0 && s.escolha < s.opcoes.Count && s.opcoes[s.escolha] != null)
            {
                Undo.RecordObject(s.opcoes[s.escolha], "Trocar peca");
                s.opcoes[s.escolha].SetActive(false);
            }
            if (idx >= 0 && s.opcoes[idx] != null)
            {
                Undo.RecordObject(s.opcoes[idx], "Trocar peca");
                s.opcoes[idx].SetActive(true);
            }
            s.escolha = idx;

            if (propagar && espelhar && s.par >= 0)
            {
                var o = slots[s.par];
                Definir(o, Mathf.Min(idx, o.opcoes.Count - 1), false, false);
            }

            if (focar) Enquadrar(s.focoY, s.focoDist);
            precisaAssar = true;
            Sujar();
        }

        void TrocarGenero(bool novoFeminino)
        {
            if (novoFeminino == feminino) return;
            string sai = novoFeminino ? "Male_Parts" : "Female_Parts";
            foreach (var s in slots)
                if (s.grupo == sai && s.escolha >= 0) Definir(s, -1, false, false);
            feminino = novoFeminino;
            Base();
            Enquadrar(0.95f, 3.4f);
            Avisar(feminino ? "Corpo feminino." : "Corpo masculino.", MessageType.Info);
        }

        void Aleatorio()
        {
            if (alvo == null) return;
            foreach (var s in slots)
            {
                if (!Visivel(s)) continue;
                if (espelhar && s.par >= 0 && s.chave.Contains("Left")) continue;
                if (s.obrigatorio) Definir(s, Random.Range(0, s.opcoes.Count), true, false);
                else Definir(s, Random.value < 0.35f ? Random.Range(0, s.opcoes.Count) : -1, true, false);
            }
            Enquadrar(0.95f, 3.4f);
            Avisar("Personagem aleatorio. Ajuste o que quiser por cima.", MessageType.Info);
        }

        void CarregarPreset(string caminho)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(caminho);
            if (p == null) { Avisar("Preset nao carregou.", MessageType.Error); return; }
            if (alvo == null) NovoPersonagem();

            var ligados = new HashSet<string>();
            foreach (var smr in p.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (LigadoAteRaiz(smr.transform, p.transform)) ligados.Add(smr.name);

            AplicarNomes(ligados);
            Enquadrar(0.95f, 3.4f);
            Avisar("Preset carregado (" + ligados.Count + " pecas). Agora e' so' alterar.", MessageType.Info);
        }

        static bool LigadoAteRaiz(Transform t, Transform raiz)
        {
            while (t != null && t != raiz)
            {
                if (!t.gameObject.activeSelf) return false;
                t = t.parent;
            }
            return true;
        }

        void AplicarNomes(HashSet<string> ligados)
        {
            int m = 0, f = 0;
            foreach (var n in ligados)
            {
                if (n.Contains("_Male_")) m++;
                else if (n.Contains("_Female_")) f++;
            }
            if (m > 0 || f > 0) feminino = f > m;

            foreach (var s in slots)
            {
                int achado = -1;
                for (int i = 0; i < s.opcoes.Count; i++)
                    if (ligados.Contains(s.opcoes[i].name)) { achado = i; break; }
                Definir(s, achado, false, false);
            }
        }

        // ------------------------------------------------------------------ armas
        List<Transform> ListarArmas()
        {
            var r = new List<Transform>();
            if (alvo == null) return r;
            var raiz = alvo.transform.Find("Root");
            if (raiz == null) return r;

            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            {
                bool eOsso = false;
                for (int i = 0; i < OSSOS.Length; i++) if (t.name == OSSOS[i]) { eOsso = true; break; }
                if (!eOsso) continue;
                foreach (Transform filho in t)
                    if (filho.GetComponentInChildren<Renderer>(true) != null) r.Add(filho);
            }
            return r;
        }

        void Encaixar()
        {
            if (alvo == null) return;
            if (armaSel <= 0) { Avisar("Escolha uma arma na lista primeiro.", MessageType.Warning); return; }

            var osso = AcharOsso(OSSOS[ossoSel]);
            if (osso == null) { Avisar("Nao achei o osso " + OSSOS[ossoSel] + ".", MessageType.Error); return; }
            var pre = AssetDatabase.LoadAssetAtPath<GameObject>(armasCaminhos[armaSel]);
            if (pre == null) { Avisar("Arma nao carregou.", MessageType.Error); return; }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(pre, osso);
            // Duas armadilhas do esqueleto da Synty:
            //  1. os ossos da mao tem o Y apontando pra BAIXO, entao com rotacao local zero a
            //     arma nasce de ponta-cabeca. Alinhar com a rotacao do personagem deixa ela em
            //     pe' (as armas da Synty sao modeladas com o cabo no eixo Y);
            //  2. os ossos vem do FBX com escala 0.01. Com localScale 1 a arma sai 100x menor
            //     (uma lanca de 2,33 m vira 2,3 cm e some). Compensar pela escala do osso.
            go.transform.position = osso.position;
            go.transform.rotation = alvo.transform.rotation;
            go.transform.localScale = EscalaCompensada(osso);
            Undo.RegisterCreatedObjectUndo(go, "Encaixar arma");

            precisaAssar = true;
            Enquadrar(1.15f, 2.4f);
            Sujar();
            Avisar("Encaixada em " + osso.name + " (" + Comprimento(pre).ToString("F2") +
                   " m), em pe' na mao. Ajuste posicao/rotacao nos campos abaixo.", MessageType.Info);
        }

        /// <summary>localScale que anula a escala do osso, pra arma sair no tamanho real.</summary>
        static Vector3 EscalaCompensada(Transform osso)
        {
            var e = osso.lossyScale;
            return new Vector3(
                Mathf.Abs(e.x) > 1e-6f ? 1f / e.x : 1f,
                Mathf.Abs(e.y) > 1e-6f ? 1f / e.y : 1f,
                Mathf.Abs(e.z) > 1e-6f ? 1f / e.z : 1f);
        }

        static float Comprimento(GameObject arma)
        {
            float maior = 0f;
            foreach (var mf in arma.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var t = mf.sharedMesh.bounds.size;
                maior = Mathf.Max(maior, Mathf.Max(t.x, Mathf.Max(t.y, t.z)));
            }
            return maior;
        }

        Transform AcharOsso(string nomeOsso)
        {
            var raiz = alvo.transform.Find("Root");
            if (raiz == null) return null;
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                if (t.name == nomeOsso) return t;
            return null;
        }

        // ------------------------------------------------------------------ utilidades
        static void GarantirPasta(string caminho)
        {
            if (AssetDatabase.IsValidFolder(caminho)) return;
            var partes = caminho.Split('/');
            string acumulado = partes[0];
            for (int i = 1; i < partes.Length; i++)
            {
                string proximo = acumulado + "/" + partes[i];
                if (!AssetDatabase.IsValidFolder(proximo)) AssetDatabase.CreateFolder(acumulado, partes[i]);
                acumulado = proximo;
            }
        }

        void FocarNaScene()
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv != null) sv.FrameSelected();
        }

        void Sujar()
        {
            if (alvo == null) return;
            EditorUtility.SetDirty(alvo);
            if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(alvo.scene);
            Repaint();
            SceneView.RepaintAll();
        }

        void Avisar(string texto, MessageType tipo)
        {
            aviso = texto;
            avisoTipo = tipo;
            Repaint();
        }
    }
}
