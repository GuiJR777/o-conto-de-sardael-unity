using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using Sardael;

namespace SardaelEditor
{
    /// <summary>
    /// Acrescenta o estado "Aparo" ao Sardael_Combate.controller.
    ///
    /// NAO reconstroi o controller. O MontarCombate, que construia tudo do zero, esta' morto
    /// (aponta pras pastas de pacote antigas). Aqui eu so' penduro UM estado a mais no que ja'
    /// existe e funciona, seguindo o mesmo padrao dos estados que ja' estao la'.
    ///
    /// O PADRAO COPIADO E' O DA ESQUIVA/ROLAR, medido no proprio controller:
    ///   entrada  — de AnyState, condicao If no gatilho, hasExitTime 0, mistura 0,05 s fixa,
    ///              canTransitionToSelf 0
    ///   saida    — pra Locomocao, sem condicao, hasExitTime 1, exitTime 1,0 ("a animacao
    ///              acabou"), mistura 0,12 s fixa
    /// Nenhuma duracao de acao e' escolhida por mim: quem termina o aparo e' o clipe.
    ///
    /// DOIS CUIDADOS QUE CUSTARAM CARO NESTE PROJETO E QUE ESTAO RESPEITADOS AQUI:
    ///
    /// 1. O clipe e' o "- Hit", NAO o "- Loop". O pacote traz os dois; o "- Loop" tem
    ///    loopTime ligado, e clipe que repete NUNCA termina — e terminar e' exatamente o
    ///    evento que faz sair do estado em todo o resto deste controller. O "- Hit" ja' vem
    ///    com loopTime desligado, entao entra na disciplina da casa sem que eu precise mexer
    ///    em nenhum .meta dentro de _Pacotes (que se perderia numa reimportacao da loja).
    ///
    /// 2. A raiz dos dois clipes de aparo vem TRAVADA (Bake Into Pose ligado nos tres eixos).
    ///    Isso aqui NAO e' defeito: aparo e' no lugar, e travado e' o que a gente quer. Nao
    ///    rode um "Destravar" por reflexo — foi o destravar que fez o golpe empurrar, e aqui
    ///    ele faria o heroi escorregar enquanto apara.
    ///
    /// A tag "acao" faz o resto de graca: com ela o MovimentoDoHeroi para de andar, nao vira
    /// de lado, nao pula e nao encadeia outra habilidade enquanto o aparo roda.
    ///
    /// Rode quantas vezes quiser: se o Aparo ja' existir, ele e' refeito, nao duplicado.
    /// </summary>
    public static class MontarAparo
    {
        const string CTRL  = "Assets/_Sardael/Animacoes/Controladores/Sardael_Combate.controller";

        // caminho COMPLETO de proposito: o pacote tem oito quase-homonimos (Parry1H, Parry2H,
        // ParryDW, e a versao feminina do mesmo ParryPolearm01). Procurar por nome pega o errado.
        const string CLIPE = "Assets/_Pacotes/Kevin Iglesias/Human Animations/Animations/"
                           + "Male/Combat/Polearm/HumanM@ParryPolearm01 - Hit.fbx";

        const string ESTADO = "Aparo";
        const string VOLTA  = "Locomocao";

        [MenuItem("Sardael/Montar Aparo (estado + gatilho)")]
        public static void Executar()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[Aparo] pare o Play primeiro."); return; }

            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CTRL);
            if (ctrl == null) { Debug.LogError("[Aparo] nao achei " + CTRL); return; }

            var clipe = Clipe(CLIPE);
            if (clipe == null) { Debug.LogError("[Aparo] nao achei clipe em " + CLIPE); return; }

            var camada = ctrl.layers[0].stateMachine;

            // ---------------------------------------------------------- gatilho
            // confere NOME E TIPO: AddParameter nao lanca com nome repetido, ele inventa um
            // "aparar 0" que ninguem seta. E se "aparar" um dia existir como Float, a condicao
            // If montada abaixo seria invalida — melhor gritar do que montar torto.
            if (!TemParametro(ctrl, Duelo.P_APARAR, AnimatorControllerParameterType.Trigger))
            {
                if (TemParametroComOutroTipo(ctrl, Duelo.P_APARAR))
                {
                    Debug.LogError("[Aparo] ja' existe um parametro \"" + Duelo.P_APARAR
                                 + "\" que NAO e' Trigger. Conserte no controller antes.");
                    return;
                }
                ctrl.AddParameter(Duelo.P_APARAR, AnimatorControllerParameterType.Trigger);
            }

            // ---------------------------------------------------------- o estado
            var aparo = Achar(camada, ESTADO);
            bool nasceuAgora = aparo == null;
            if (nasceuAgora) aparo = camada.AddState(ESTADO);

            aparo.motion = clipe;
            aparo.speed = 1f;
            aparo.tag = MovimentoDoHeroi.TAG_ACAO;
            aparo.writeDefaultValues = true;       // igual aos outros estados deste controller

            var locomocao = Achar(camada, VOLTA);
            if (locomocao == null) { Debug.LogError("[Aparo] nao achei o estado " + VOLTA); return; }

            // ---------------------------------------------------------- as ligacoes
            // limpo antes de ligar: rodar de novo nao pode empilhar transicao repetida
            foreach (var t in camada.anyStateTransitions)
                if (t.destinationState == aparo) camada.RemoveAnyStateTransition(t);
            while (aparo.transitions.Length > 0) aparo.RemoveTransition(aparo.transitions[0]);

            // entrada: de qualquer lugar, no quadro em que o gatilho acende
            var entra = camada.AddAnyStateTransition(aparo);
            entra.AddCondition(AnimatorConditionMode.If, 0f, Duelo.P_APARAR);
            entra.hasExitTime = false;
            entra.exitTime = 0.75f;               // inerte com hasExitTime 0; e' o valor que os outros guardam
            entra.hasFixedDuration = true;
            entra.duration = 0.05f;
            entra.offset = 0f;
            entra.canTransitionToSelf = false;    // apertar de novo nao reinicia o aparo

            // saida: quando a animacao acabar, e so' por isso
            var sai = aparo.AddTransition(locomocao);
            sai.hasExitTime = true;
            sai.exitTime = 1f;
            sai.hasFixedDuration = true;
            sai.duration = 0.12f;
            sai.offset = 0f;
            sai.canTransitionToSelf = true;

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();

            Debug.Log(string.Format(
                "[Aparo] estado {0} no {1}.\n"
              + "  clipe: {2}  ({3:F2} s, loop={4})\n"
              + "  tag: \"{5}\"   gatilho: \"{6}\"\n"
              + "  entra de AnyState (mistura 0,05 s), sai pra {7} no fim do clipe (mistura 0,12 s)\n"
              + "  Agora rode: Sardael > Rodar Teste do Aparo",
                nasceuAgora ? "criado" : "refeito", CTRL,
                clipe.name, clipe.length, clipe.isLooping,
                MovimentoDoHeroi.TAG_ACAO, Duelo.P_APARAR, VOLTA));

            if (clipe.isLooping)
                Debug.LogWarning("[Aparo] o clipe escolhido esta' em LOOP. Ele nunca vai terminar, "
                               + "e a saida do estado depende de terminar. Confira se o caminho "
                               + "nao caiu no \"- Loop\" em vez do \"- Hit\".");
        }

        // ------------------------------------------------------------------ ajudantes

        /// <summary>O AnimationClip de dentro do FBX, pulando o __preview__ que o Unity cria.</summary>
        static AnimationClip Clipe(string caminho)
        {
            var tudo = AssetDatabase.LoadAllAssetsAtPath(caminho);
            if (tudo == null || tudo.Length == 0) return null;
            foreach (var a in tudo)
            {
                var c = a as AnimationClip;
                if (c != null && !c.name.StartsWith("__preview__")) return c;
            }
            return null;
        }

        static bool TemParametro(AnimatorController ctrl, string nome, AnimatorControllerParameterType tipo)
        {
            foreach (var p in ctrl.parameters) if (p.name == nome && p.type == tipo) return true;
            return false;
        }

        static bool TemParametroComOutroTipo(AnimatorController ctrl, string nome)
        {
            foreach (var p in ctrl.parameters)
                if (p.name == nome && p.type != AnimatorControllerParameterType.Trigger) return true;
            return false;
        }

        static AnimatorState Achar(AnimatorStateMachine camada, string nome)
        {
            foreach (var c in camada.states) if (c.state.name == nome) return c.state;
            return null;
        }
    }
}
