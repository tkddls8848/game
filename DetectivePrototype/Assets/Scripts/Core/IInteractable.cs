namespace Detective.Core
{
    /// <summary>
    /// 플레이어가 E 키로 상호작용할 수 있는 대상.
    /// UnityEngine 타입을 노출하지 않아서(§18-1) 상호작용 규칙 자체는 순수 C#으로 남는다.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>화면 하단에 띄울 안내문. 예: "[E] 와인잔 조사".</summary>
        string InteractionPrompt { get; }

        /// <summary>지금 상호작용이 가능한가(이미 조사가 끝난 대상 등은 false).</summary>
        bool CanInteract { get; }

        void Interact();
    }
}
