using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SliderControl : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    public bool is_Touched = false;
    public bool is_Outed = false;

    public Button PauseButton;

    private void Awake()
    {
        if (PauseButton != null)
            PauseButton.onClick.AddListener(OnPauseButtonClick);
    }

    private void OnPauseButtonClick()
    {
        if (is_Touched)
        {
            OnPointerUp(null);
        }
        else
        {
            OnPointerDown(null);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        is_Touched = true;
        SetPauseButtonText("��");
        Debug.Log("Pause");
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        is_Touched = false;
        is_Outed = true;
        SetPauseButtonText("��");
        Debug.Log("Play");
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null || eventData.button == PointerEventData.InputButton.Left)
        {
            is_Touched = !is_Touched;
            is_Outed = false;
            SetPauseButtonText(is_Touched ? "��" : "��");
        }
    }

    private void SetPauseButtonText(string text)
    {
        if (PauseButton == null)
            return;

        Text label = PauseButton.GetComponentInChildren<Text>();
        if (label != null)
            label.text = text;
    }
}
