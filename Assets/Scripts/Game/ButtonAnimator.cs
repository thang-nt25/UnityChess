
using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class ButtonAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    // Scale animation
    private Vector3 originalScale;
    private Coroutine currentAnimation;
    private bool isPointerOver = false;
    private bool isPointerDown = false;

    [Header("Animation Settings")]
    [SerializeField, Range(1f, 1.5f)]
    private float hoverScale = 1.1f;

    [SerializeField, Range(0.5f, 1f)]
    private float clickScale = 0.95f;

    [SerializeField, Range(0.01f, 1f)]
    private float animationDuration = 0.15f;

    // Audio
    private AudioSource audioSource;
    [Header("Audio Settings")]
    [SerializeField] private AudioClip hoverSound;
    [SerializeField] private AudioClip clickSound;


    private void Awake()
    {
        originalScale = transform.localScale;
        SetupAudioSource();
    }

    private void SetupAudioSource()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isPointerOver = true;
        if (!isPointerDown)
        {
            AnimateScale(originalScale * hoverScale);
        }
        PlaySound(hoverSound);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerOver = false;
        if (!isPointerDown)
        {
            AnimateScale(originalScale);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPointerDown = true;
        AnimateScale(originalScale * clickScale);
        PlaySound(clickSound);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPointerDown = false;
        if (isPointerOver)
        {
            AnimateScale(originalScale * hoverScale);
        }
        else
        {
            AnimateScale(originalScale);
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    private void AnimateScale(Vector3 targetScale)
    {
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
        }
        currentAnimation = StartCoroutine(ScaleCoroutine(targetScale));
    }

    private IEnumerator ScaleCoroutine(Vector3 targetScale)
    {
        Vector3 startScale = transform.localScale;
        float timer = 0f;

        while (timer < animationDuration)
        {
            timer += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(timer / animationDuration);
            transform.localScale = Vector3.Lerp(startScale, targetScale, progress);
            yield return null;
        }

        transform.localScale = targetScale;
    }

    private void OnDisable()
    {
        // Reset scale when the button is disabled or the object is deactivated
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
        }
        transform.localScale = originalScale;
        isPointerOver = false;
        isPointerDown = false;
    }
}
