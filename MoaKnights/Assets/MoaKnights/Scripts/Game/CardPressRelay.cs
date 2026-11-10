using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MoaKnights.Game
{
    /// <summary>카드 한 장의 누름을 CardBar로 넘긴다.</summary>
    public sealed class CardPressRelay : MonoBehaviour, IPointerDownHandler
    {
        public Action Pressed;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) Pressed?.Invoke();
        }
    }
}
