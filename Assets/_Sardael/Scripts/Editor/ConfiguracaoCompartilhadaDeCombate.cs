using Sardael;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SardaelEditor
{
    /// <summary>
    /// Configuracao de authoring compartilhada entre a Sandbox e encontros da campanha.
    /// O runtime recebe componentes e referencias ja serializados; ele nao depende de
    /// AssetDatabase nem recria a montagem da Sandbox ao carregar uma cena.
    /// </summary>
    public static class ConfiguracaoCompartilhadaDeCombate
    {
        const string ClipAparo =
            "Assets/_Pacotes/Kevin Iglesias/Human Animations/Animations/Male/Combat/Polearm/HumanM@ParryPolearm01 - Hit.fbx";

        public sealed class HeroiConfigurado
        {
            public Animator animator;
            public MovimentoDoHeroi movimento;
            public EntradaDeCombate entrada;
            public CombateDoHeroi combate;
            public DefesaDoHeroi defesa;
            public VidaDoHeroi vida;
        }

        public static HeroiConfigurado ConfigurarHeroi(
            GameObject heroi,
            AnimatorController controller,
            InputActionAsset input,
            RegistroDeCombate registro,
            ResolvedorDeImpacto resolvedor,
            SistemaDeFlow flow,
            DiretorDeCombate diretor,
            DefinicaoDeAtaque[] definicoes,
            bool removerLegado)
        {
            if (heroi == null) return null;
            if (removerLegado) RemoverLegado(heroi);

            var animator = ObterOuAdicionar<Animator>(heroi);
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var trocador = ObterOuAdicionar<TrocadorDeAnimacoes>(heroi);
            trocador.Configurar(animator);
            TrocadorDeAnimacoesEditor.Sincronizar(trocador);
            var clipeAparo = CarregarClip(ClipAparo);
            TrocadorDeAnimacoesEditor.DefinirSubstituicao(trocador, "Aparo", clipeAparo);
            TrocadorDeAnimacoesEditor.DefinirReversao(trocador, "Aparo", true);

            var controlador = ObterOuAdicionar<CharacterController>(heroi);
            controlador.center = new Vector3(0f, 0.9f, 0f);
            controlador.height = 1.8f;
            controlador.radius = 0.3f;

            var movimento = ObterOuAdicionar<MovimentoDoHeroi>(heroi);
            var entrada = ObterOuAdicionar<EntradaDeCombate>(heroi);
            entrada.Configurar(input);
            var combate = ObterOuAdicionar<CombateDoHeroi>(heroi);
            combate.Configurar(movimento, entrada, registro, resolvedor, animator, definicoes);
            var defesa = ObterOuAdicionar<DefesaDoHeroi>(heroi);
            defesa.Configurar(entrada, combate, movimento, animator, flow, diretor, 0.6f);
            var vida = ObterOuAdicionar<VidaDoHeroi>(heroi);
            vida.Configurar(movimento, animator, flow, diretor, defesa);
            MontarMovimento.EncaixarLanca(heroi);

            Marcar(animator, controlador, movimento, entrada, combate, defesa, vida, trocador);
            return new HeroiConfigurado
            {
                animator = animator,
                movimento = movimento,
                entrada = entrada,
                combate = combate,
                defesa = defesa,
                vida = vida
            };
        }

        public static SistemaDeExecucao ConfigurarExecucao(
            GameObject heroi,
            HeroiConfigurado configurado,
            RegistroDeCombate registro,
            SistemaDeFlow flow,
            DiretorDeCombate diretor,
            CameraDeCombate cameraDeCombate,
            RuntimeAnimatorController controladorDaDerrubadaDoExecutor,
            RuntimeAnimatorController controladorDaDerrubadaDaVitima,
            VarianteDeExecucao[] variantes)
        {
            if (heroi == null || configurado == null) return null;
            var execucao = ObterOuAdicionar<SistemaDeExecucao>(heroi);
            execucao.Configurar(
                configurado.movimento,
                configurado.entrada,
                registro,
                flow,
                diretor,
                configurado.animator,
                cameraDeCombate,
                controladorDaDerrubadaDoExecutor,
                controladorDaDerrubadaDaVitima,
                variantes);
            Marcar(execucao);
            return execucao;
        }

        public static AlvoDeCombate ConfigurarInimigo(
            GameObject inimigo,
            RuntimeAnimatorController controller,
            RegistroDeCombate registro,
            Transform jogador,
            GameObject fxSangue,
            GameObject cabecaDecepada,
            AnimationClip[] animacoesDeMorte,
            bool removerLegado,
            bool ativoAoCarregar)
        {
            if (inimigo == null) return null;
            if (removerLegado) RemoverLegado(inimigo);

            var animator = ObterOuAdicionar<Animator>(inimigo);
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var controlador = ObterOuAdicionar<CharacterController>(inimigo);
            controlador.center = new Vector3(0f, 0.9f, 0f);
            controlador.height = 1.8f;
            controlador.radius = 0.35f;
            controlador.skinWidth = 0.04f;
            foreach (var colisor in inimigo.GetComponents<Collider>())
                if (colisor != controlador) Object.DestroyImmediate(colisor);

            var reacao = ObterOuAdicionar<ReacaoDeCombate>(inimigo);
            reacao.Configurar(animator);
            var motor = ObterOuAdicionar<MotorDeCombateDoInimigo>(inimigo);
            motor.Configurar(reacao);
            var alvo = ObterOuAdicionar<AlvoDeCombate>(inimigo);
            alvo.Configurar(registro, reacao, motor);
            alvo.AnimacoesDeMorte = animacoesDeMorte;
            alvo.SumirAposMorte = true;
            alvo.SegundosAteSumir = 5f;

            var barra = ObterOuAdicionar<BarraDeVidaDoInimigo>(inimigo);
            barra.Visivel = ativoAoCarregar;
            var indicador = ObterOuAdicionar<IndicadorDeExecucao>(inimigo);
            indicador.Configurar(alvo, jogador);
            var efeito = ObterOuAdicionar<EfeitoDeFinalizacao>(inimigo);
            efeito.automaticoPorAnimacao = false;
            efeito.repetirNoLoop = false;
            efeito.instante = 0.62f;
            efeito.fxSangue = fxSangue;
            efeito.decapitar = cabecaDecepada != null;
            efeito.prefabDaCabeca = cabecaDecepada;
            efeito.quantidade = 12f;
            efeito.tamanho = 3.2f;
            efeito.alcance = 2f;

            alvo.enabled = ativoAoCarregar;
            motor.enabled = ativoAoCarregar;
            controlador.enabled = ativoAoCarregar;
            Marcar(animator, controlador, reacao, motor, alvo, barra, indicador, efeito);
            return alvo;
        }

        public static void RemoverLegado(GameObject raiz)
        {
            foreach (var duelo in raiz.GetComponentsInChildren<Duelo>(true))
                Object.DestroyImmediate(duelo);
            foreach (var teste in raiz.GetComponentsInChildren<TesteDeCombate>(true))
                Object.DestroyImmediate(teste);
            foreach (var corpo in raiz.GetComponentsInChildren<CorpoDoOrc>(true))
                Object.DestroyImmediate(corpo);
            foreach (var tocador in raiz.GetComponentsInChildren<TocadorDeClipe>(true))
                Object.DestroyImmediate(tocador);
            foreach (var combatente in raiz.GetComponentsInChildren<Combatente>(true))
                Object.DestroyImmediate(combatente);
        }

        static T ObterOuAdicionar<T>(GameObject alvo) where T : Component
        {
            var componente = alvo.GetComponent<T>();
            return componente != null ? componente : alvo.AddComponent<T>();
        }

        static AnimationClip CarregarClip(string caminho)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(caminho))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    return clip;
            return null;
        }

        static void Marcar(params Object[] objetos)
        {
            foreach (var objeto in objetos)
                if (objeto != null) EditorUtility.SetDirty(objeto);
        }
    }
}
