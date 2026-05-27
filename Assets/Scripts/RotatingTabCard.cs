using UnityEngine;
using UnityEngine.EventSystems;

public class RotatingTabCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler
{
    public RotatingTabCarousel carousel;
    public RectTransform card;

    public void OnPointerEnter(PointerEventData eventData)
    {
        EnsureCard();

        if (carousel != null)
            carousel.PointerEntered(card);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        EnsureCard();

        if (carousel != null)
            carousel.PointerExited(card);
    }

    public void OnSelect(BaseEventData eventData)
    {
        EnsureCard();

        if (carousel != null)
            carousel.FocusFromNavigation(card);
    }

    private void EnsureCard()
    {
        if (card == null)
            card = transform as RectTransform;
    }
}
