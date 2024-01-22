using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

public class Section : MonoBehaviour
{
    public delegate void SectionCallBack(Section section);
    public event SectionCallBack Loaded;

    private List<PickUp> _pickUps = new List<PickUp>();

    [Header("Respawn Point")]
    [SerializeField]
    private List<GameObject> _checkPoints = new List<GameObject>();
    public List<GameObject> Checkpoints => _checkPoints;
    
    [SerializeField]
    private Transform _pickupsParentPickUps;
    public Transform PickupsParent => _pickupsParentPickUps;
    [SerializeField]
    private GameObject _parentEnvironment;
    public GameObject ParentEnvironment => _parentEnvironment;


    public List<PickUp> PickUps => _pickUps;


    #region Private Functions

    private void OnEnable()
    {
        _pickUps = GetComponentsInChildren<PickUp>().ToList();

        // delaying the logic allow the pickupManager to properly subscribe to Loaded
        StartCoroutine(DelayEnable());      
    }

    IEnumerator DelayEnable()
    {
        yield return new WaitForSeconds(0.5f);

        Loaded?.Invoke(this);
    }

    #endregion
}
