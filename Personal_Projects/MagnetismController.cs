using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Highlighters;
using UnityCore.Audio;
using UnityEngine;

public class MagnetismController : MonoBehaviour
{
    [Header("Charged Objects")]
    public List<FingerCharge> ChargedObjects = new List<FingerCharge>();
    public List<FingerCharge> ChargedPositiveObjects = new List<FingerCharge>();
    public List<FingerCharge> ChargedNegativeObjects = new List<FingerCharge>();

    [Header("Charged Centre")]
    public Centroid MagnetismCentroid;

    private bool _centroidCreated;
    private bool _moveCentroid; 
    private bool _centroidStartedExpulsionRoutine;

    [HideInInspector]
    public List<FingerCharge> ChargedObjectsInCentroid = new List<FingerCharge>();
    [HideInInspector]
    public List<FingerCharge> ChargedObjectsOutsideCentroid = new List<FingerCharge>();

    [Header("Variables")]
    [SerializeField]
    private float _centroidMoveSpeed = 50;
    [SerializeField]
    private float _centroidThicknessBufferForParticles = 1.5f;
    public float CentroidThicknessBufferForParticles => _centroidThicknessBufferForParticles;

    [SerializeField]
    private float _expulsionTimeBeforeAddingDrag = 0.05f;
    [SerializeField]
    private float _expulsionTimeWithDrag = 0.5f;
    [SerializeField]
    private float _inverseExpulsionTimeBeforeAddingDrag = 0.05f;
    [SerializeField]
    private float _inverseExpulsionTimeWithDrag = 0.5f;

    [SerializeField]
    private float _inverseDischargeDelay = 0.32f;
    [SerializeField]
    private float _inverseDischargeRadius = 15;
    [SerializeField]
    private float _inverseDischargeStrength = 2000;
    private ParticleObjectPool _poolToTakeFromInverseDischarge;

    [SerializeField]
    private float _particleIntervalTime = 3f;
    [SerializeField]
    private float _particleTimeToReachTarget = 5f;
    private float _particleSpawnTimer;

    [Header("Shader Setup Left")]
    [SerializeField]
    private Color _outlineNegative;
    [SerializeField]
    private Color _overlayFrontNeg;
    [SerializeField]
    private Color _overlayBackNeg;
    [Header("Shader Setup Right")]
    [SerializeField]
    private Color _outlinePositive;
    [SerializeField]
    private Color _overlayFrontPos;
    [SerializeField]
    private Color _overlayBackPos;

    [Header("Audio")]
    [SerializeField] private AudioElement _soundExpulsion;
    [SerializeField] private AudioElement _soundChargedObject;
    [SerializeField] private AudioElement _soundInverseDischarge;

    private Vector3 _totalPositionCharge;
    private float _totalCharge;
    private Vector3 _totalPosition;

    private int _layerUnshakeable;
    private int _layerShakeable;

    private ParticleObjectPool _particlePoolMagnetism;
    private List<ParticleMagnetism> _activeParticlesMagnetism = new List<ParticleMagnetism>();


    private void Start()
    {
        _layerUnshakeable = 7;
        _layerShakeable = 8;

        ShowCentroid(false);
        AllowCentroidMovement(false,false);
        AllowMagnetismUpdate(false);
    }
    private void Update()
    {
        // maybe only call this if the magnitude (difference) is great ?
        CalculateSimpleCentroidMagnetizingVectors();

        // show particle moving from charged object to centroid
        AffixMagnetismParticles();
    }


    public void FingerHitTryCharge(Finger fingerThatHitSomething, Collision collision, GameObject hitObject, Vector3 impactVelocity)
    {
        // 1) check layer hit object
        if (collision.gameObject.layer == _layerShakeable)
        {
            // 2) Parent --- make sure the hit object has uniform scale 1.1.1 !!!
            GameManager.Instance.TransformController.SetParent(fingerThatHitSomething.transform, collision.transform);

            // 3) add FingerCharge --- only add the charge-script if it does not alrdy have one !
            FingerCharge fingerChargeOfInterest = TryGetFingerCharge(hitObject);
            fingerChargeOfInterest.AddCharge(fingerThatHitSomething.Hand.TypeHand, fingerThatHitSomething, fingerThatHitSomething.ChargeStrength);

            // 4) check for charge amount and act accordingly
            GameManager.Instance.MagnetismController.FingerChargeInteraction(fingerThatHitSomething, fingerChargeOfInterest,
                ref fingerChargeOfInterest.IsCharged,
                ref fingerChargeOfInterest.MyCharge,
                fingerChargeOfInterest.ChargeThreshHoldValue,
                fingerThatHitSomething.Hand.TypeHand,
                ref fingerChargeOfInterest.PreviousChargeType,
                collision, impactVelocity);
        }
        // 5) we hit terrain --- -> charge the finger that is stuck in place ?
        else
        {
            // ...
        }

        // 6) check magnetism
        CheckForPossibleMagnetismCentroid();
    }


    public void FingerChargeInteraction(Finger fingerThatHit, FingerCharge fingerCharge, ref bool isCharged, ref float myCharge, float chargeThreshHoldValue, 
        HandType typeHand, ref HandType previousChargeType,
        Collision collision, Vector3 impactVelocity)
    {
        //   4.1) an object that is [uncharged] and about to be [charged] from the current charge increase
        if (isCharged == false && Mathf.Abs(myCharge) >= chargeThreshHoldValue)
        {
            GameManager.Instance.HandsController.FingerHitImpact(fingerThatHit, fingerCharge, collision, impactVelocity);

            FullyChargeObject(fingerCharge);
            FullyChargedAddForce(fingerCharge);       
            isCharged = true;

            GameManager.Instance.AudioController.PlayAudio(_soundChargedObject, false, 0, this.transform);
        }
        //   4.2) an object that is [charged] and receiving an opposite charge, instantly [dis-charging] it (explosion ?) 
        else if (isCharged == true && typeHand != previousChargeType)
        {
            GameManager.Instance.HandsController.FingerHitInverseEffect(fingerCharge);

            // similar to expulsion, create an explosion
            isCharged = false;
            myCharge = 0;
            StartCoroutine(InverseDischargeRoutine(fingerThatHit, fingerCharge));         
        }
        //   4.3) an object that is [charged] and receiving additional charge that it likes
        else if (isCharged == true)
        {
            GameManager.Instance.HandsController.FingerHitImpact(fingerThatHit, fingerCharge, collision, impactVelocity);

            // ...
        }
        //   4.4) an object that is [uncharged] and still won't be [charged]
        else
        {
            GameManager.Instance.HandsController.FingerHitImpact(fingerThatHit, fingerCharge, collision, impactVelocity);

            // ...
        }

        // 4.9) set previous charge type
        previousChargeType = typeHand;
    }


    public void FullyChargeObject(FingerCharge objectThatGotCharged)
    {
        // add to the list of charged objects that can be interacted with
        ChargedObjects.Add(objectThatGotCharged);
        ChargedObjectsOutsideCentroid.Add(objectThatGotCharged);

        // add to specific lists, assign correct colors
        if (objectThatGotCharged.MyCharge > 0)
        {
            ChargeShader(objectThatGotCharged, false);
            ChargedPositiveObjects.Add(objectThatGotCharged);
        }
        else
        {
            ChargeShader(objectThatGotCharged, true);
            ChargedNegativeObjects.Add(objectThatGotCharged);
        }
    }
    public void CentroidAddition(FingerCharge fingerCharge)
    {
        // freeze the object in place... 
        GameManager.Instance.PhysicsController.RigidbodySetVelocityZero(fingerCharge.MyRigidbody);
        GameManager.Instance.PhysicsController.RigidbodyBoolKinematic(fingerCharge.MyRigidbody, true);
        GameManager.Instance.PhysicsController.ColliderBool(fingerCharge.MyChargeThreshHold.MyCollider, false);
        // parent it to the centroid
        GameManager.Instance.TransformController.SetParent(fingerCharge.transform, MagnetismCentroid.CentroidVisualParent.transform);

        // as soon as 1 is added, start the expulsion routine (shrinking, shaking, physics, sound coroutine)
        if (_centroidStartedExpulsionRoutine == false)
        {
            StartCoroutine(CentroidExpulsionRoutine());

            _centroidStartedExpulsionRoutine = true;
        }

        // add to list
        ChargedObjectsInCentroid.Add(fingerCharge);
        fingerCharge.AddedToCentroid = true;
        fingerCharge.CanAddToCentroid = false;
        // remove from other list
        ChargedObjectsOutsideCentroid.Remove(fingerCharge);
    }


    public void AddParticleMagnetism(ParticleMagnetism particleMagnet)
    {
        _activeParticlesMagnetism.Add(particleMagnet);
    }
    public void RemoveParticleMagnetism(ParticleMagnetism particleMagnet)
    {
        _activeParticlesMagnetism.Remove(particleMagnet);
    }
    public void UpdateActiveParticleDirections()
    {
        for (int i = 0; i < _activeParticlesMagnetism.Count; i++)
        {
            _activeParticlesMagnetism[i].ReInitialize(MagnetismCentroid.transform.position);
        }
    }


    private FingerCharge TryGetFingerCharge(GameObject hitObject)
    {
        if (hitObject.TryGetComponent(out FingerCharge fingerCharge) == true)
        {
            return fingerCharge;
        }
        else
        {
            FingerCharge fingerChargeAdded = hitObject.AddComponent<FingerCharge>();
            return fingerChargeAdded;
        }
    }
    private void FullyChargedAddForce(FingerCharge fingerChargeHitObject)
    {
        Vector3 forceUpwards = Vector3.up * GameManager.Instance.PhysicsController.ChargeForceUpStrength;
        GameManager.Instance.PhysicsController.RigidbodyAddForce(fingerChargeHitObject.MyRigidbody, forceUpwards);
        GameManager.Instance.PhysicsController.RigidbodySetDrag(fingerChargeHitObject.MyRigidbody, true);
        GameManager.Instance.PhysicsController.RigidbodyBoolGravity(fingerChargeHitObject.MyRigidbody, false);
    }
    private void CalculateWeightedCentroidMagnetizingVectors()
    {
        //// calculate weighted centroid
        //for (int i = 0; i < ChargedObjects.Count; i++)
        //{
        //    float newCharge = Mathf.Abs(ChargedObjects[i].MyCharge);
        //    Vector3 newPositionCharge = ChargedObjects[i].transform.position * newCharge;

        //    _totalPositionCharge += newPositionCharge;
        //    _totalCharge += newCharge;
        //}
        //Vector3 supposedCentroid = _totalPositionCharge / _totalCharge;

        //// position / move the centroid
        //if (_centroidCreated == true)
        //{
        //    if (_moveCentroid == true)
        //    {
        //        // move towards target position
        //        MagnetismCentroid.transform.position = Vector3.MoveTowards(MagnetismCentroid.transform.position, supposedCentroid, Time.deltaTime * _centroidMoveSpeed);
        //        Debug.Log("movin");
        //    }
        //}
        //else
        //{
        //    // instant appearance at correct position
        //    Debug.Log("tp centroid to centre");
        //    MagnetismCentroid.transform.position = supposedCentroid;
        //}

        //// reset charges
        //_totalPositionCharge = Vector3.zero;
        //_totalCharge = 0;

        //// centroid has been created atleast once
        //_centroidCreated = true;
    }
    private void CalculateSimpleCentroidMagnetizingVectors()
    {
        // calculate centroid
        for (int i = 0; i < ChargedObjects.Count; i++)
        {
            _totalPosition += ChargedObjects[i].transform.position;
        }
        Vector3 supposedCentroid = _totalPosition / ChargedObjects.Count;

        // position / move the centroid
        if (_centroidCreated == true)
        {
            if (_moveCentroid == true)
            {
                // move towards target position
                MagnetismCentroid.transform.position = Vector3.MoveTowards(MagnetismCentroid.transform.position, supposedCentroid, Time.deltaTime * _centroidMoveSpeed);
            }
        }
        else
        {
            // instant appearance at correct position
            MagnetismCentroid.transform.position = supposedCentroid;
            ShowCentroid(true);
            AllowCentroidMovement(true, true);
        }

        // reset charges
        _totalPosition = Vector3.zero;
    }
    public void StopMagnetism()
    {
        ShowCentroid(false);

        AllowCentroidMovement(false,false);
    }
    public void CalculateMagnetism()
    {
        // set particle timer to max as to instantly have them appear (only on if magnetism just now got enabled)
        if (_centroidCreated == false)
        {
            _particleSpawnTimer = _particleIntervalTime;
        }

        CalculateSimpleCentroidMagnetizingVectors();
    }


    private void CheckForPossibleMagnetismCentroid()
    {
        if (ChargedPositiveObjects.Count > 0 && ChargedNegativeObjects.Count > 0)
        {
            CalculateMagnetism();
        }
        else
        {
            StopMagnetism();
        }
    }
    private void ShowCentroid(bool isShown)
    {
        MagnetismCentroid.gameObject.SetActive(isShown);
        _centroidCreated = isShown;
    }
    private void AllowCentroidMovement(bool canMove, bool magnetismStillPossible)
    {
        _moveCentroid = canMove;
        AllowMagnetismUpdate(magnetismStillPossible);
    }


    private void ChargeShader(FingerCharge objectThatGotCharged, bool isNegative = true)
    {
        if (isNegative == true)
        {
            objectThatGotCharged.MyHighlighter.Settings.MeshOutlineFront.Color = _outlineNegative;

            objectThatGotCharged.MyHighlighter.Settings.OverlayFront.Color = _overlayFrontNeg;
            objectThatGotCharged.MyHighlighter.Settings.OverlayBack.Color = _overlayBackNeg;
        }
        else
        {
            objectThatGotCharged.MyHighlighter.Settings.MeshOutlineFront.Color = _outlinePositive;

            objectThatGotCharged.MyHighlighter.Settings.OverlayFront.Color = _overlayFrontPos;
            objectThatGotCharged.MyHighlighter.Settings.OverlayBack.Color = _overlayBackPos;
        }

        objectThatGotCharged.MyHighlighter.enabled = true;
    }
    private void DischargeShader(FingerCharge objectThatGotCharged)
    {
        objectThatGotCharged.MyHighlighter.enabled = false;
    }

    private void AffixMagnetismParticles()
    {
        _particleSpawnTimer += Time.deltaTime;

        if (_particleSpawnTimer >= _particleIntervalTime)
        {
            for (int i = 0; i < ChargedObjects.Count; i++)
            {
                // activate particle at charged-object position
                ParticleEntity particleEntity = GameManager.Instance.ParticleManager.CreateParticleWorldSpaceMagnetism
                    (ParticleType.MagnemtismRoute, ref _particlePoolMagnetism, ChargedObjects[i].transform.position, Quaternion.identity);

                // get magnetizing component... 
                ParticleMagnetism particleMagnet = particleEntity.GetComponent<ParticleMagnetism>();
                // add to list
                AddParticleMagnetism(particleMagnet);
                // assign values
                particleMagnet.Initialize(MagnetismCentroid.transform.position, ChargedObjects[i], _particleTimeToReachTarget);
            }
            _particleSpawnTimer = 0;
        }    
    }
    private void AffixMagnetizeDirections()
    {
        for (int i = 0; i < ChargedObjects.Count; i++)
        {
            ChargedObjects[i].MagnetizeDirection = (MagnetismCentroid.transform.position - ChargedObjects[i].transform.position).normalized;
            ChargedObjects[i].MagnetizeSpeed = Mathf.Abs(ChargedObjects[i].MyCharge);
        }
    }
    private void FixDragOnChargedObjectsOutsideCentroid()
    {
        for (int i = 0; i < ChargedObjectsOutsideCentroid.Count; i++)
        {
            GameManager.Instance.PhysicsController.AddGroundedChecker(ChargedObjectsOutsideCentroid[i].MyRigidbody);
        }
    }
    private void ClearChargedObjectsLists()
    {
        // destroying finger charge + clearing lists (NEEDS TO HAPPEN AFTER EXPULSION)
        while (ChargedObjects.Count > 0)
        {
            ChargedObjects[ChargedObjects.Count - 1].ResetMyFingerChargeBools();

            Destroy(ChargedObjects[ChargedObjects.Count - 1]);
            ChargedObjects.RemoveAt(ChargedObjects.Count - 1);
        }
        ChargedObjects.Clear();
        ChargedNegativeObjects.Clear();
        ChargedPositiveObjects.Clear();
        ChargedObjectsInCentroid.Clear();
        ChargedObjectsOutsideCentroid.Clear();
    }
    private void ClearMagnetismCalculatedCharge()
    {
        // reset charges (NEEDS TO HAPPEN AFTER EXPULSION)
        _totalPositionCharge = Vector3.zero;
        _totalCharge = 0;
        _centroidStartedExpulsionRoutine = false;
    }
    private void AllowMagnetismUpdate(bool isEnabled)
    {
        // disable this script(update) (NEEDS TO HAPPEN AFTER EXPULSION)
        this.enabled = isEnabled;
    }



    public IEnumerator MagnetismRoutine()
    {
        AffixMagnetizeDirections();

        yield return new WaitForSeconds(GameManager.Instance.HandsController.TimeToReachClap);

        // if magnetism is not possible due to a lacking charged object quantity... un-charge all the objects that are charged and default them        
        if (ChargedPositiveObjects.Count == 0 || ChargedNegativeObjects.Count == 0)
        {
            for (int i = 0; i < ChargedObjects.Count; i++)
            {
                ChargedObjects[i].MyHighlighter.enabled = false;
            }

            for (int i = 0; i < ChargedObjects.Count; i++)
            {
                // unparent + reset the scale to 1
                GameManager.Instance.TransformController.SetParentNull(ChargedObjects[i].transform);
                GameManager.Instance.TransformController.SetScaleToOne(ChargedObjects[i].transform);

                // adjust their rigidbodies !
                GameManager.Instance.PhysicsController.ColliderBool(ChargedObjects[i].MyChargeThreshHold.MyCollider, true);
                GameManager.Instance.PhysicsController.RigidbodyBoolKinematic(ChargedObjects[i].MyRigidbody, false);
                GameManager.Instance.PhysicsController.RigidbodyBoolConstraints(ChargedObjects[i].MyRigidbody, RigidbodyConstraints.None);
                if (ChargedObjects[i].MyChargeThreshHold.HitActivatesGravity == true)
                {
                    GameManager.Instance.PhysicsController.RigidbodyBoolGravity(ChargedObjects[i].MyRigidbody, true);
                }

                // first set drags to 0... then add grounded checker
                GameManager.Instance.PhysicsController.RigidbodySetDrag(ChargedObjects[i].MyRigidbody, false, true);
                GameManager.Instance.PhysicsController.AddGroundedChecker(ChargedObjects[i].MyRigidbody);
            }

            // reset bools
            ClearChargedObjectsLists();
            ClearMagnetismCalculatedCharge();
            AllowMagnetismUpdate(false);

            yield break;
        }

        // stop moving the centroid
        _moveCentroid = false;

        // activate all magnetism paths present in the scene on the moment of the clap (coroutine it)
        for (int i = 0; i < ChargedObjects.Count; i++)
        {
            GameManager.Instance.PhysicsController.RigidbodyBoolConstraints(ChargedObjects[i].MyRigidbody, RigidbodyConstraints.None);
            GameManager.Instance.PhysicsController.RigidbodyBoolGravity(ChargedObjects[i].MyRigidbody, false);

            // reset the drag to 0 on all of the objects
            GameManager.Instance.PhysicsController.RigidbodySetDrag(ChargedObjects[i].MyRigidbody, false, true);

            // set velocity of attraction to centroid
            // (weighted with charges below)
            //Vector3 magnetizeVelocity = ChargedObjects[i].MagnetizeDirection * ChargedObjects[i].MagnetizeSpeed * GameManager.Instance.PhysicsController.MagnetizeVelocity; 
            Vector3 magnetizeVelocity = ChargedObjects[i].MagnetizeDirection * GameManager.Instance.PhysicsController.MagnetizeVelocity;
            GameManager.Instance.PhysicsController.RigidbodySetVelocity(ChargedObjects[i].MyRigidbody, magnetizeVelocity);

            // set bool so it can attach to centroid
            ChargedObjects[i].CanAddToCentroid = true;

            // disable and enable the collider in seperate frames in case the objects were already inside the centroid trigger
            StartCoroutine(ColliderTogglerRoutine(ChargedObjects[i].MyChargeThreshHold.MyCollider));
        }

        // disable active particles showing trails
        for (int i = 0; i < _activeParticlesMagnetism.Count; i++)
        {
            GameManager.Instance.MagnetismController.StartCoroutine(EndMagnetismParticleRoutine(_activeParticlesMagnetism[i]));
        }
    }
    public IEnumerator InverseDischargeRoutine(Finger fingerThatHit, FingerCharge fingerCharge)
    {
        // remove from lists
        ChargedObjects.Remove(fingerCharge);
        ChargedNegativeObjects.Remove(fingerCharge);
        ChargedPositiveObjects.Remove(fingerCharge);

        // create particle for discharge
        GameManager.Instance.ParticleManager.CreateParticleWorldSpace(ParticleType.InverseDischarge, ref _poolToTakeFromInverseDischarge, fingerThatHit.transform.position, Quaternion.identity);

        // create force explosion at finger location
        List<Collider> nearbyColliders = Physics.OverlapSphere(fingerCharge.transform.position, _inverseDischargeRadius).ToList();

        yield return new WaitForSeconds(_inverseDischargeDelay);

        // play inverse discharge clip
        GameManager.Instance.AudioController.PlayAudio(_soundInverseDischarge, false, 0, fingerCharge.transform);

        // add drag function from expulsion
        StartCoroutine(RigidbodyInverseExpulsionRoutine(fingerCharge.MyRigidbody));

        // add explosion force to nearby rigids
        for (int i = 0; i < nearbyColliders.Count; i++)
        {
            Rigidbody rigidNearby = nearbyColliders[i].GetComponent<Rigidbody>();
            if (rigidNearby != null)
            {
                rigidNearby.AddExplosionForce(_inverseDischargeStrength, fingerThatHit.transform.position, _inverseDischargeRadius);
            }
        }

        // enable gravity on hit object again
        if (fingerCharge.MyChargeThreshHold.HitActivatesGravity == true)
        {
            GameManager.Instance.PhysicsController.RigidbodyBoolGravity(fingerCharge.MyRigidbody, true);
        }

        // remove shader visual
        DischargeShader(fingerCharge);
    }
    public IEnumerator EndMagnetismParticleRoutine(ParticleMagnetism particleMagnetism)
    {
        // stop Magnetism_0
        particleMagnetism.Magnetism_0.Stop();

        yield return new WaitForSeconds(particleMagnetism.MyParticleEntity.TimeToDisable);

        // remove from list
        GameManager.Instance.MagnetismController.RemoveParticleMagnetism(particleMagnetism);
        // end particle
        particleMagnetism.gameObject.SetActive(false);
    }


    /// <summary>
    /// IEnumerator which will increase the drag of a rigidbody drastically shortly after expulsion, to then revert the drag to nothing afterwards.
    /// </summary>
    /// <param name="rigidbody"></param>
    /// <returns></returns>
    private IEnumerator RigidbodyExpulsionRoutine(Rigidbody rigidbody)
    {
        yield return new WaitForSeconds(_expulsionTimeBeforeAddingDrag);

        GameManager.Instance.PhysicsController.RigidbodySetDrag(rigidbody, true);

        yield return new WaitForSeconds(_expulsionTimeWithDrag);

        // first set drags to 0
        GameManager.Instance.PhysicsController.RigidbodySetDrag(rigidbody, false, true);
        // .. then add grounded checker
        GameManager.Instance.PhysicsController.AddGroundedChecker(rigidbody);
        // ... if grounded checker gets grounded ONCE --> set the drag to defaults + remove the grounded checker
    }
    private IEnumerator RigidbodyInverseExpulsionRoutine(Rigidbody rigidbody)
    {
        yield return new WaitForSeconds(_inverseExpulsionTimeBeforeAddingDrag);

        GameManager.Instance.PhysicsController.RigidbodySetDrag(rigidbody, true);

        yield return new WaitForSeconds(_inverseExpulsionTimeWithDrag);

        // first set drags to 0
        GameManager.Instance.PhysicsController.RigidbodySetDrag(rigidbody, false, true);
        // .. then add grounded checker
        GameManager.Instance.PhysicsController.AddGroundedChecker(rigidbody);
        // ... if grounded checker gets grounded ONCE --> set the drag to defaults + remove the grounded checker
    }


    private IEnumerator CentroidExpulsionRoutine()
    {
        // 1) start shrinking + shaking
        MagnetismCentroid.CentroidAnimator.Play(MagnetismCentroid.AnimShrink);
        MagnetismCentroid.ShakerScript.enabled = true;

        // play audio magnetism
        GameManager.Instance.AudioController.PlayAudio(_soundExpulsion, false, 0, MagnetismCentroid.transform);

        // 2) after x time, scaleUp (NEEDS TO BE IN SYNC WITH PHYSICS EXPULSION)

        yield return new WaitForSeconds(3f);

        MagnetismCentroid.CentroidAnimator.Play(MagnetismCentroid.AnimScaleUp);
        MagnetismCentroid.ShakerScript.enabled = false;

        // reset rigidbodies + shaders
        for (int i = 0; i < ChargedObjects.Count; i++)
        {
            GameManager.Instance.PhysicsController.RigidbodyBoolGravity(ChargedObjects[i].MyRigidbody, true);

            ChargedObjects[i].MyHighlighter.enabled = false;
        }
        // check for what objects are not in the centroid, default their drags/ give them grounded script
        FixDragOnChargedObjectsOutsideCentroid();
        // each object attached to the centroid gets propelled away
        for (int i = 0; i < ChargedObjectsInCentroid.Count; i++)
        {
            // unparent + reset the scale to 1
            GameManager.Instance.TransformController.SetParentNull(ChargedObjectsInCentroid[i].transform);
            GameManager.Instance.TransformController.SetScaleToOne(ChargedObjectsInCentroid[i].transform);

            GameManager.Instance.PhysicsController.ColliderBool(ChargedObjectsInCentroid[i].MyChargeThreshHold.MyCollider, true);
            GameManager.Instance.PhysicsController.RigidbodyBoolKinematic(ChargedObjectsInCentroid[i].MyRigidbody, false);
            GameManager.Instance.PhysicsController.RigidbodyBoolConstraints(ChargedObjectsInCentroid[i].MyRigidbody, RigidbodyConstraints.None);
            GameManager.Instance.PhysicsController.RigidbodyBoolGravity(ChargedObjectsInCentroid[i].MyRigidbody, true);

            // increase drag shortly after expulsion the objects, afterwards set them back to default (coroutine this)
            StartCoroutine(RigidbodyExpulsionRoutine(ChargedObjects[i].MyRigidbody));

            // expel force
            Vector3 expulsionVelocity = (ChargedObjectsInCentroid[i].MagnetizeDirection * -1) * GameManager.Instance.PhysicsController.CentroidExpulsionForce;
            GameManager.Instance.PhysicsController.RigidbodySetVelocity(ChargedObjectsInCentroid[i].MyRigidbody, expulsionVelocity);
        }

        // 3) after y time, disable centroid object + centroidcreated bool

        yield return new WaitForSeconds(1f);

        // disable the centroid 
        ShowCentroid(false);

        // reset bools
        ClearChargedObjectsLists();
        ClearMagnetismCalculatedCharge();
        AllowMagnetismUpdate(false);
    }
    private IEnumerator ColliderTogglerRoutine(Collider collider)
    {
        collider.enabled = false;

        yield return new WaitForEndOfFrame();

        collider.enabled = true;
    }

}
