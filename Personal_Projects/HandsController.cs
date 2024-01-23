using System;
using System.Collections;
using System.Collections.Generic;
using UnityCore.Audio;
using UnityEngine;

public class HandsController : MonoBehaviour
{
    [Header("Finger Lists")]
    public List<Finger> ShotFingers = new List<Finger>();
    public List<Finger> StationedFingers = new List<Finger>();
    public List<Finger> FingersToReturn = new List<Finger>();

    [Header("Hands")]
    [SerializeField] private Hand _handLeft;
    public Hand HandLeft => _handLeft;

    [SerializeField] private Hand _handRight;
    public Hand HandRight => _handRight;

    [Header("Variables")]
    [SerializeField]
    private float _sensController = 50;
    [SerializeField]
    private float _sensMouse = 20;
    private float _sensitivityToUse;
    [SerializeField]
    private float _handRotationSpeed = 20;
    [SerializeField]
    private float _fingerReturnTime;
    public float FingerReturnTime => _fingerReturnTime;
    [SerializeField]
    private float _fingerIntervalTime;
    public float FingerIntervalTime => _fingerIntervalTime;
    [SerializeField]
    private LayerMask _layersToAimOn;

    [Header("Time-frames of animations")]
    [SerializeField]
    private float _timeToReachClap;
    public float TimeToReachClap => _timeToReachClap;

    [Header("Transforms to rotate")]
    [SerializeField]
    private Transform _cameraPivotToMoveAim;

    [Header("Audio")]
    [SerializeField] private List<AudioElement> _soundsClap = new List<AudioElement>();
    [SerializeField] private List<AudioElement> _soundsFingerShot = new List<AudioElement>();
    [SerializeField] private AudioElement _soundFingerHit;
    [SerializeField] private AudioElement _soundFingerReconnect;

    private Vector2 _aimVector;
    float xRotation;
    float yRotation;

    private int _layerFinger;

    private bool _canClap;
    private bool _isClapping;
    private bool _magnetismPossible;

    private Quaternion _initialRotationLeft;
    private Quaternion _initialRotationRight;

    private ParticleObjectPool _poolToTakeFrom;



    private void Start()
    {
        _layerFinger = 6;

        _handLeft.StationedFingerCount = 5;
        _handRight.StationedFingerCount = 5;

        // disable rigidbodies
        _handLeft.Index.RigidbodyFinger.interpolation = RigidbodyInterpolation.None;
        _handLeft.Middle.RigidbodyFinger.interpolation = RigidbodyInterpolation.None;
        _handLeft.Ring.RigidbodyFinger.interpolation = RigidbodyInterpolation.None;
        _handLeft.Pink.RigidbodyFinger.interpolation = RigidbodyInterpolation.None;
        _handLeft.Thumb.RigidbodyFinger.interpolation = RigidbodyInterpolation.None;

        _handRight.Index.RigidbodyFinger.interpolation = RigidbodyInterpolation.None;
        _handRight.Middle.RigidbodyFinger.interpolation = RigidbodyInterpolation.None;
        _handRight.Ring.RigidbodyFinger.interpolation = RigidbodyInterpolation.None;
        _handRight.Pink.RigidbodyFinger.interpolation = RigidbodyInterpolation.None;
        _handRight.Thumb.RigidbodyFinger.interpolation = RigidbodyInterpolation.None;


        // subscribe all events
        _handLeft.Index.OnFingerHit += OnFingerHitSomething;
        _handLeft.Middle.OnFingerHit += OnFingerHitSomething;
        _handLeft.Ring.OnFingerHit += OnFingerHitSomething;
        _handLeft.Pink.OnFingerHit += OnFingerHitSomething;
        _handLeft.Thumb.OnFingerHit += OnFingerHitSomething;

        _handRight.Index.OnFingerHit += OnFingerHitSomething;
        _handRight.Middle.OnFingerHit += OnFingerHitSomething;
        _handRight.Ring.OnFingerHit += OnFingerHitSomething;
        _handRight.Pink.OnFingerHit += OnFingerHitSomething;
        _handRight.Thumb.OnFingerHit += OnFingerHitSomething;

        // first bezier call
        GameManager.Instance.FingerPathManager.FirstBezierCreationCall();

        // store initial rotations
        _initialRotationLeft = _handLeft.RotationRoot.transform.localRotation;
        _initialRotationRight = _handRight.RotationRoot.transform.localRotation;

        // add all fingers to stationed
        StationedFingers.AddRange(_handLeft.MyFingers);
        StationedFingers.AddRange(_handRight.MyFingers);

        // set sensitivity
        _sensitivityToUse = _sensController;
    }
    private void Update()
    {
        MoveAim();
    }



    public void OnFingerHitSomething(Finger fingerThatHitSomething, Collision collision)
    {
        GameObject hitObject = collision.gameObject;     
        Vector3 impactVelocity = fingerThatHitSomething.RigidbodyFinger.velocity;
        // freeze the finger in place (maybe even parent it, only if it's shakeable ?)
        GameManager.Instance.PhysicsController.RigidbodySetVelocityZero(fingerThatHitSomething.RigidbodyFinger);
        GameManager.Instance.PhysicsController.RigidbodyBoolKinematic(fingerThatHitSomething.RigidbodyFinger, true);
        GameManager.Instance.PhysicsController.ColliderBool(fingerThatHitSomething.ColliderFinger, false);
        // impact and charging logic
        GameManager.Instance.MagnetismController.FingerHitTryCharge(fingerThatHitSomething, collision, hitObject, impactVelocity);

        // particle hit
        GameManager.Instance.ParticleManager.PlaySimpleParticle(fingerThatHitSomething.ParticleImpact);

        // sound
        GameManager.Instance.AudioController.PlayAudio(_soundFingerHit, false, 0, fingerThatHitSomething.transform);
    }
    public void FingerHitImpact(Finger fingerThatHit, FingerCharge objectThatGotHit, Collision collision, Vector3 impactVelocity)
    {
        Vector3 normalizedFingerVelocity = impactVelocity.normalized;
        Vector3 multipliedFingerVelocity = normalizedFingerVelocity * GameManager.Instance.PhysicsController.FingerImpactStrength;
        Vector3 massMultipliedFingerVelocity = multipliedFingerVelocity * fingerThatHit.FingerMass;

        GameManager.Instance.PhysicsController.RigidbodyAddForceAtPosition(objectThatGotHit.MyRigidbody, massMultipliedFingerVelocity, collision.GetContact(0).point, ForceMode.Impulse);

        // activate gravity if needed , don't activate it if the object stays charged
        if (objectThatGotHit.MyChargeThreshHold.HitActivatesGravity == true && objectThatGotHit.IsCharged == false)
        {
            GameManager.Instance.PhysicsController.RigidbodyBoolGravity(objectThatGotHit.MyRigidbody, true);
        }
    }
    public void FingerHitInverseEffect(FingerCharge objectThatGotHit)
    {
        // freeze the object in place
        GameManager.Instance.PhysicsController.RigidbodySetVelocityZero(objectThatGotHit.MyRigidbody);
        GameManager.Instance.PhysicsController.RigidbodySetAngularVelocityZero(objectThatGotHit.MyRigidbody);
    }
    public void Shoot(Hand handThatShoots)
    {
        if (GameManager.Instance.IsPaused == true || _isClapping == true)
        {
            return;
        }

        // get the targets position in world space
        Vector3 targetPoint = GetTarget();

        // rotate hand to look at target
        handThatShoots.LookAtTarget(targetPoint);

        // recoil
        handThatShoots.AnimateHandRecoil();

        // animates the Palm, fingers accordingly
        ShootCorrectFingerWithAnimation(handThatShoots, targetPoint);

        // enter the next animation for the next finger
        StartCoroutine(EnterNextAnimationsRoutine(handThatShoots));
    }
    public void ClapHands()
    {
        if (GameManager.Instance.IsPaused == true)
        {
            return;
        }

        // possibly change the logic for when I can clap and magnetize...
        if (_handLeft.StationedFingerCount < 5 || _handRight.StationedFingerCount < 5)
        {
            _canClap = true;
        }
        else
        {
            _canClap = false;
        }


        if (_canClap == true && _isClapping == false)
        {
            _isClapping = true;

            // reset the look rotation of the hands and animate them
            StartCoroutine(ResetHandRotationRoutine(_handLeft));
            StartCoroutine(ResetHandRotationRoutine(_handRight));
            _handLeft.Palm.AnimatePalmCall(_handLeft.Palm.AnimClap);
            _handRight.Palm.AnimatePalmCall(_handRight.Palm.AnimClap);

            // stretch any fingers that are still present on the hand
            for (int i = 0; i < StationedFingers.Count; i++)
            {
                StationedFingers[i].AnimateFingerCall(StationedFingers[i].AnimStretched);
            }

            // activate actual magnetism
            StartCoroutine(GameManager.Instance.MagnetismController.MagnetismRoutine());            
            StartCoroutine(ReturnFingersAndResetHandsRoutine());

            // play audio clap
            GameManager.Instance.AudioController.PlayRandomAudio(_soundsClap, false, (TimeToReachClap - 0.09f));
        }
    }
    public void DetectAimController(Vector2 mouseDelta)
    {
        _aimVector = mouseDelta;

        GameManager.Instance.IsUsingMouse = false;

        AdjustSensitivity();

        if (GameManager.Instance.IsPaused == true)
        {
            _aimVector = Vector2.zero;
        }
    }
    public void DetectAimMouse(Vector2 mouseDelta)
    {
        _aimVector = mouseDelta;

        GameManager.Instance.IsUsingMouse = true;

        AdjustSensitivity();

        if (GameManager.Instance.IsPaused == true)
        {
            _aimVector = Vector2.zero;
        }
    }
    public IEnumerator RetractFingerRoutine(Finger fingerToRetract)
    {
        // unparent + reset scale
        GameManager.Instance.TransformController.SetFingerTransformValuesForReturn(fingerToRetract);

        // stop any velocity/physics related to this finger
        GameManager.Instance.PhysicsController.RigidbodySetVelocityZero(fingerToRetract.RigidbodyFinger);
        GameManager.Instance.PhysicsController.RigidbodyBoolKinematic(fingerToRetract.RigidbodyFinger, true);
        GameManager.Instance.PhysicsController.ColliderBool(fingerToRetract.ColliderFinger, false);

        // disable shot trail & enable return trail
        GameManager.Instance.ParticleManager.SetTrailStatus(fingerToRetract.TrailsShot, false);
        GameManager.Instance.ParticleManager.SetTrailStatus(fingerToRetract.TrailsReturn, true);

        // create initial path & keep updating the path on this finger (in manager)
        GameManager.Instance.FingerPathManager.CreateFirstFramePath(fingerToRetract);
        GameManager.Instance.FingerPathManager.EnablePathUpdating(true);

        // set the speed on the pathFollower
        GameManager.Instance.FingerPathManager.SetSpeedFingerPath(fingerToRetract);

        // play sound reconnection (with delay)
        GameManager.Instance.AudioController.PlayAudio(_soundFingerReconnect, false, _fingerReturnTime - 0.1f);

        // once the finger has arrived...
        yield return new WaitForSeconds(FingerReturnTime);

        GameManager.Instance.FingerPathManager.ResetAndDisablePath(fingerToRetract);

        ReconnectFingerVisually(fingerToRetract);

        // update lists and values
        GameManager.Instance.FingerPathManager.RemoveRetractFingerPath(fingerToRetract);

        ReplenishFinger(fingerToRetract);
    }


    private void MoveAim()
    {
        // only execute below code if my _aimVector is not 0,0
        xRotation += _aimVector.y * Time.deltaTime * _sensitivityToUse;
        yRotation += _aimVector.x * Time.deltaTime * _sensitivityToUse;
        xRotation = Mathf.Clamp(xRotation, -45f, 80f); 

        _cameraPivotToMoveAim.localEulerAngles = new Vector3(xRotation, yRotation, 0);

        // player visuals should also rotate like this
        if (GameManager.Instance.IsFirstPerson == true)
        {
            GameManager.Instance.Player.PlayerVisual.transform.localEulerAngles = new Vector3(0, yRotation, 0);
        }
    }
    private void AdjustSensitivity()
    {
        if (GameManager.Instance.IsUsingMouse == false)
        {
            _sensitivityToUse = _sensController;
        }
        else
        {
            _sensitivityToUse = _sensMouse;
        }
    }
    private void ShootCorrectFingerWithAnimation(Hand handThatShoots, Vector3 targetPoint)
    {
        Finger fingerToShoot = null;

        switch (handThatShoots.StationedFingerCount)
        {
            case 5:
                // INDEX
                fingerToShoot = handThatShoots.Index;
                // palm pose 
                handThatShoots.Palm.AnimatePalmCall(handThatShoots.Palm.AnimPoseIndexInstant);
                // finger pose 
                handThatShoots.Index.AnimateFingerCall(handThatShoots.Index.AnimStretchedInstant);
                handThatShoots.Middle.AnimateFingerCall(handThatShoots.Middle.AnimPoseIndexInstant);
                handThatShoots.Ring.AnimateFingerCall(handThatShoots.Ring.AnimPoseIndexInstant);
                handThatShoots.Pink.AnimateFingerCall(handThatShoots.Pink.AnimPoseIndexInstant);
                handThatShoots.Thumb.AnimateFingerCall(handThatShoots.Thumb.AnimPoseIndexInstant);

                break;
            case 4:
                // MIDDLE
                fingerToShoot = handThatShoots.Middle;
                // palm pose 
                handThatShoots.Palm.AnimatePalmCall(handThatShoots.Palm.AnimPoseMiddleInstant);
                // finger pose 
                handThatShoots.Middle.AnimateFingerCall(handThatShoots.Middle.AnimStretchedInstant);
                handThatShoots.Ring.AnimateFingerCall(handThatShoots.Ring.AnimPoseMiddleInstant);
                handThatShoots.Pink.AnimateFingerCall(handThatShoots.Pink.AnimPoseMiddleInstant);
                handThatShoots.Thumb.AnimateFingerCall(handThatShoots.Thumb.AnimPoseMiddleInstant);

                break;
            case 3:
                // RING
                fingerToShoot = handThatShoots.Ring;
                // palm pose 
                handThatShoots.Palm.AnimatePalmCall(handThatShoots.Palm.AnimPoseRingInstant);
                // finger pose 
                handThatShoots.Ring.AnimateFingerCall(handThatShoots.Ring.AnimStretchedInstant);
                handThatShoots.Pink.AnimateFingerCall(handThatShoots.Pink.AnimPoseRingInstant);
                handThatShoots.Thumb.AnimateFingerCall(handThatShoots.Thumb.AnimPoseRingInstant);

                break;
            case 2:
                // PINK
                fingerToShoot = handThatShoots.Pink;
                // palm pose 
                handThatShoots.Palm.AnimatePalmCall(handThatShoots.Palm.AnimPosePinkInstant);
                // finger pose 
                handThatShoots.Pink.AnimateFingerCall(handThatShoots.Pink.AnimStretchedInstant);
                handThatShoots.Thumb.AnimateFingerCall(handThatShoots.Thumb.AnimPosePinkInstant);

                break;
            case 1:

                return;
        }

        // disconnect finger and shoot finger
        fingerToShoot.DisconnectFinger();
        ShootFinger(fingerToShoot, targetPoint);
        handThatShoots.StationedFingerCount -= 1;    
    }
    private void ShootFinger(Finger finger, Vector3 targetPoint)
    {
        // cast a ray from the fingerSlot to the 'targetPoint' (or just get direction)
        Vector3 directionToShoot = (targetPoint - finger.Slot.position).normalized;

        // shoot the actual rigidbody of the finger
        GameManager.Instance.PhysicsController.ColliderBool(finger.ColliderFinger, true);
        GameManager.Instance.PhysicsController.RigidbodyBoolKinematic(finger.RigidbodyFinger, false);
        GameManager.Instance.PhysicsController.RigidbodySetVelocity(finger.RigidbodyFinger, directionToShoot * GameManager.Instance.PhysicsController.ShootingVelocity);

        // update lists
        ShotFingers.Add(finger);
        StationedFingers.Remove(finger);        
        GameManager.Instance.FingerPathManager.AddToShotList(finger);

        // play muzzle particle & activate trail
        GameManager.Instance.ParticleManager.PlaySimpleParticle(finger.ParticleMuzzle);
        GameManager.Instance.ParticleManager.SetTrailStatus(finger.TrailsShot, true);

        // play audio shot
        GameManager.Instance.AudioController.PlayRandomAudio(_soundsFingerShot, false, 0);
    }
    private Vector3 GetTarget()
    {
        // raycast centre of screen to get a 'target' world position
        Vector3 targetPoint = Vector3.zero;
        Ray ray = Camera.main.ScreenPointToRay(new Vector3((Screen.width / 2), (Screen.height / 2), 0));
        RaycastHit hit;

        // if there is a 'target'...
        if (Physics.Raycast(ray, out hit, Mathf.Infinity, _layersToAimOn) == true)
        {
            targetPoint = hit.point;
        }
        else // if no 'target -> shoot towards a set 'fakeTarget'
        {
            // more logic here...
            targetPoint = Camera.main.transform.position + Camera.main.transform.forward * 100;
        }

        return targetPoint;
    }
    private void ResetHandsAndFingersToIdle()
    {
        _handRight.Palm.AnimatePalmTrigger(_handRight.Palm.TriggerIdle);
        _handLeft.Palm.AnimatePalmTrigger(_handLeft.Palm.TriggerIdle);
        for (int i = 0; i < StationedFingers.Count; i++)
        {
            var fingerOfInterest = StationedFingers[i];
            fingerOfInterest.AnimateFingerCall(fingerOfInterest.AnimIdle);
        }
    }
    private void ReconnectFingerVisually(Finger fingerThatReturned)
    {
        fingerThatReturned.Reconnect();
        fingerThatReturned.AnimateFingerCall(fingerThatReturned.AnimRecover);
        GameManager.Instance.ParticleManager.SetTrailStatus(fingerThatReturned.TrailsReturn, false);
    }
    private void ReplenishFinger(Finger fingerToReplenish)
    {
        ShotFingers.Remove(fingerToReplenish);        
        StationedFingers.Add(fingerToReplenish);

        if (fingerToReplenish.Hand.TypeHand == HandType.Left)
        {
            _handLeft.StationedFingerCount += 1;
        }
        else
        {
            _handRight.StationedFingerCount += 1;
        }
    }
    private IEnumerator ReturnFingersAndResetHandsRoutine()
    {
        // fill up fingers to return list
        for (int i = ShotFingers.Count -1 ; i >= 0; i--)
        {
            FingersToReturn.Add(ShotFingers[i]);
        }

        yield return new WaitForSeconds(_timeToReachClap); 

        // return fingers 1 by 1
        while (FingersToReturn.Count > 0)
        {
            StartCoroutine(RetractFingerRoutine(FingersToReturn[FingersToReturn.Count - 1]));
            FingersToReturn.Remove(FingersToReturn[FingersToReturn.Count - 1]);

            // have wait time between each finger
            yield return new WaitForSeconds(_fingerIntervalTime);          
        }

        float returnTimeFingers = FingerReturnTime + 0.1f;
        yield return new WaitForSeconds(returnTimeFingers);

        // have all fingers and palms enter idle again
        ResetHandsAndFingersToIdle();

        yield return new WaitForSeconds(1);  

        _isClapping = false;
    }
    private IEnumerator EnterNextAnimationsRoutine(Hand handThatShot)
    {
        yield return new WaitForSeconds(0.15f);

        switch (handThatShot.StationedFingerCount)
        {
            case 4:
                handThatShot.Palm.AnimatePalmCall(handThatShot.Palm.AnimPoseMiddle);

                handThatShot.Middle.AnimateFingerTrigger(handThatShot.Middle.TriggerStretch);
                handThatShot.Ring.AnimateFingerCall(handThatShot.Ring.AnimPoseMiddle);
                handThatShot.Pink.AnimateFingerCall(handThatShot.Pink.AnimPoseMiddle);
                handThatShot.Thumb.AnimateFingerCall(handThatShot.Thumb.AnimPoseMiddle); 
                break;
            case 3:
                handThatShot.Palm.AnimatePalmCall(handThatShot.Palm.AnimPoseRing);

                handThatShot.Ring.AnimateFingerTrigger(handThatShot.Ring.TriggerStretch);
                handThatShot.Pink.AnimateFingerCall(handThatShot.Pink.AnimPoseRing);
                handThatShot.Thumb.AnimateFingerCall(handThatShot.Thumb.AnimPoseRing);
                break;
            case 2:
                handThatShot.Palm.AnimatePalmCall(handThatShot.Palm.AnimPosePink);

                handThatShot.Pink.AnimateFingerTrigger(handThatShot.Pink.TriggerStretch);
                handThatShot.Thumb.AnimateFingerCall(handThatShot.Thumb.AnimPosePink);
                break;
            case 1:
                handThatShot.Palm.AnimatePalmTrigger(handThatShot.Palm.TriggerIdle);

                handThatShot.Thumb.AnimateFingerTrigger(handThatShot.Thumb.TriggerIdle);

                // reset the transform that was previously looking at the target due to LookAt
                StartCoroutine(ResetHandRotationRoutine(handThatShot));
                break;
            default:

                break;
        }      
    }
    private IEnumerator ResetHandRotationRoutine(Hand handToReset, bool instant = false)
    {
        // create variables for a quaternion lerp
        bool reachedTarget = false;
        float timeCount = 0;
        float interpolationRatio = 0;
        Quaternion fromRotation = handToReset.RotationRoot.transform.localRotation;
        Quaternion toRotation;
        if (handToReset.TypeHand == HandType.Left)
        {
            toRotation = _initialRotationLeft;
        }
        else
        {
            toRotation = _initialRotationRight;
        }


        // keep rotating until target is reached
        if (instant == false)
        {
            while (reachedTarget == false)
            {
                interpolationRatio = timeCount * _handRotationSpeed;
                handToReset.RotationRoot.transform.localRotation = Quaternion.Lerp(fromRotation, toRotation, interpolationRatio);
                timeCount = timeCount + Time.deltaTime;

                if (interpolationRatio >= 1)
                {
                    reachedTarget = true;
                    handToReset.RotationRoot.transform.localRotation = toRotation;
                }

                yield return new WaitForEndOfFrame();
            }
        }
        else
        {
            handToReset.RotationRoot.transform.localRotation = toRotation;
        }
    }

}
