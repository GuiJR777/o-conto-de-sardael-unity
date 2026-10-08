using UnityEngine;

namespace Sardael
{
    public sealed class JanelaDeParry
    {
        float expiraEm;
        bool consumida = true;

        public void Abrir(float agora, float duracao)
        {
            expiraEm = agora + Mathf.Max(0f, duracao);
            consumida = false;
        }

        public bool Ativa(float agora) => !consumida && agora <= expiraEm;

        public bool TentarConsumir(float agora)
        {
            if (!Ativa(agora)) return false;
            consumida = true;
            return true;
        }

        public void Fechar() => consumida = true;
    }
}
