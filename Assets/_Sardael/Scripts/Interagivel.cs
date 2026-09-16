using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

namespace Sardael
{
    /// <summary>
    /// Marca um objeto como "da pra interagir": um aldeao pra conversar, um bau, um posto de
    /// quest. Quem procura e' o <see cref="Interacao"/> no jogador.
    ///
    /// A lista e' estatica e se mantem sozinha no OnEnable/OnDisable. Sem colisor, sem camada
    /// de fisica, sem OverlapSphere por quadro: numa cidade com dezenas de NPC, varrer uma
    /// lista de trinta itens custa menos que uma consulta de fisica, e nao depende de ninguem
    /// lembrar de por um trigger no prefab.
    /// </summary>
    public class Interagivel : MonoBehaviour
    {
        public static readonly List<Interagivel> Todos = new List<Interagivel>();

        [Header("Quem e")]
        public string nome = "Aldeao";
        [Tooltip("O que aparece no aviso: Falar, Abrir, Pegar...")]
        public string verbo = "Falar";

        [Header("Conversa")]
        [TextArea(2, 4)]
        public string[] falas = { "Bom dia, forasteiro." };
        [Tooltip("Falas de depois que a quest fecha. Vazio = repete as de cima.")]
        [TextArea(2, 4)]
        public string[] falasDepois;

        [Header("Alcance")]
        public float alcance = 2.6f;

        [Header("Quest")]
        public string quest = "";
        [Tooltip("Marca a quest como resolvida quando a conversa terminar.")]
        public bool resolveAoFalar = false;

        [Header("Evento")]
        public UnityEvent aoInteragir;

        public bool Concluido { get; set; }

        public string[] FalasDeAgora
        {
            get
            {
                if (Concluido && falasDepois != null && falasDepois.Length > 0) return falasDepois;
                return falas;
            }
        }

        void OnEnable() { if (!Todos.Contains(this)) Todos.Add(this); }
        void OnDisable() { Todos.Remove(this); }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, alcance);
        }
    }
}
