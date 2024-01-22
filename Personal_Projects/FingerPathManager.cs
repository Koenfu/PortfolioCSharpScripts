using PathCreation.Examples;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class FingerPathManager : MonoBehaviour
{
    [Header("Smoother startup with warmup 1 finger")]
    [SerializeField]
    private RetractFinger _firstFinger;

    [Header("Retract fingers")]
    [SerializeField] private List<RetractFinger> _retractFingersLeft = new List<RetractFinger>();
    [SerializeField] private List<RetractFinger> _retractFingersRight = new List<RetractFinger>();

    [Header("RetractFingers that got Shot (runtime !)")]
    [SerializeField]
    private List<RetractFinger> _retractFingersOfShotOnes = new List<RetractFinger>();

    private int _counter;


    void Update()
    {
        _counter++;
        if (_counter >= (_retractFingersOfShotOnes.Count + 1))
        {
            _counter = 1;
        }

        _retractFingersOfShotOnes[_counter - 1].UpdatePath();
    }


    public void EnablePathUpdating(bool enableThis)
    {
        this.enabled = enableThis;
    }

    // create a bezier curve instantly on startup, to smoothen future runtime
    public void FirstBezierCreationCall()
    {
        _firstFinger.CreateInitialPath();
    }

    // called the moment we shoot a finger
    public void AddToShotList(Finger finger)
    {
        _retractFingersOfShotOnes.Add(finger.RetractObjectFinger);
    }

    // called the initial momment a finger requires getting recalled
    public void CreateFirstFramePath(Finger finger)
    {
        finger.RetractObjectFinger.CreateInitialPath();
    }


    // called the moment the finger arrives back in the slot (reaching the end of the path)
    public void RemoveRetractFingerPath(Finger finger)
    {
        // remove from list
        _retractFingersOfShotOnes.Remove(finger.RetractObjectFinger);

        // if this list has nothing, stop updating this script
        if (_retractFingersOfShotOnes.Count <= 0)
        {
            EnablePathUpdating(false);
        }
    }

    public void ResetAndDisablePath(Finger fingerWhoHasTraveledItsPath)
    {
        fingerWhoHasTraveledItsPath.RetractObjectFinger.enabled = false;
        fingerWhoHasTraveledItsPath.PathFollowerFinger.enabled = false;
        fingerWhoHasTraveledItsPath.PathFollowerFinger.ResetDistance();
    }


    public void SetSpeedFingerPath(Finger fingerWhosPathNeedsToBeSet)
    {
        fingerWhosPathNeedsToBeSet.MyPathLength = fingerWhosPathNeedsToBeSet.RetractObjectFinger.MyPathCreator.path.cumulativeLengthAtEachVertex[fingerWhosPathNeedsToBeSet.RetractObjectFinger.MyPathCreator.path.cumulativeLengthAtEachVertex.Length - 1];
        fingerWhosPathNeedsToBeSet.PathFollowerFinger.Speed = (fingerWhosPathNeedsToBeSet.MyPathLength / GameManager.Instance.HandsController.FingerReturnTime);
        fingerWhosPathNeedsToBeSet.PathFollowerFinger.enabled = true;
    }

    public RetractFinger AssignPathFollower(Finger finger)
    {
        HandType typeHand = finger.Hand.TypeHand;
        FingerType typeFinger = finger.TypeFinger;

        if (typeHand == HandType.Left)
        {
            for (int i = 0; i < _retractFingersLeft.Count; i++)
            {
                if (typeFinger == _retractFingersLeft[i].FingerType)
                {
                    return _retractFingersLeft[i];
                }
            }
        }
        else
        {
            for (int i = 0; i < _retractFingersRight.Count; i++)
            {
                if (typeFinger == _retractFingersRight[i].FingerType)
                {
                    return _retractFingersRight[i];
                }
            }
        }

        return null;
    }
}
