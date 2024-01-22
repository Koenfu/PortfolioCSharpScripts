using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelSectionCreator : MonoBehaviour
{
    [Header("check if I am the first Section")]
    [SerializeField]
    private bool _isStartingSection;
    public bool IsStartingSection => _isStartingSection;

    [Header("Assign children with box colliders")]
    [SerializeField]
    private List<BoxCollider> _collidersDefiningMySection = new List<BoxCollider>();
    public List<BoxCollider> CollidersDefiningMySection => _collidersDefiningMySection;

    [SerializeField]
    private BoxCollider _colliderTail;
    public BoxCollider ColliderTail => _colliderTail;

    [SerializeField]
    private BoxCollider _colliderHead;
    public BoxCollider ColliderHead => _colliderHead;


    [Header("Assign the Section child")]
    [SerializeField]
    private Section _levelSectionChild;
    public Section Section => _levelSectionChild;
}
