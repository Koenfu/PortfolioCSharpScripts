using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AutoJumpEntryTrigger : MonoBehaviour
{
    [SerializeField]
    private AutoJumpMaster _autoJumpMaster;

    [SerializeField]
    private BoxCollider _entryCollider;
    [SerializeField]
    private BoxCollider _exitCollider;

    [SerializeField]
    private GameObject _spawnCentre, _spawnLeft, _spawnRight;

    private bool _onCooldown;

    private Vector3 _boxColSizeXEntry;
    private Vector3 _boxColEdgeXEntry;
    private Vector3 _boxColSizeXExit;
    private Vector3 _boxColEdgeXExit;

    private Vector3 _boxColSizeZEntry;
    private Vector3 _boxColEdgeZEntry;
    private Vector3 _boxColSizeZExit;
    private Vector3 _boxColEdgeZExit;

    private Vector3 _positionLeftEntry; // left => 0
    private Vector3 _positionRightEntry; // right => 1
    private Vector3 _positionForwardEntry; 
    private Vector3 _positionBackwardEntry; 

    private Vector3 _positionLeftExit; 
    private Vector3 _positionRightExit;
    private Vector3 _positionForwardExit;
    private Vector3 _positionBackwardExit;

    private Vector3 _exitPositionPlayer;
    private Vector3 _entryPositionPlayer;

    private float _percentageEntryX;
    private float _valuePosEntryX;

    private float _percentageEntryZ;
    private float _valuePosEntryZ;

    private float _valuePosExitX;
    private float _valuePosExitZ;



    private void Start()
    {
        // do the below calculations in editor with a tool and assign them (performance improvement) //
        _boxColSizeXEntry = new Vector3(_entryCollider.size.x * transform.localScale.x, 0, 0);
        _boxColEdgeXEntry = _boxColSizeXEntry / 2;
        _boxColSizeXExit = new Vector3(_exitCollider.size.x * _exitCollider.transform.localScale.x, 0, 0);
        _boxColEdgeXExit = _boxColSizeXExit / 2;

        _boxColSizeZEntry = new Vector3(0, 0, _entryCollider.size.x * transform.localScale.x);
        _boxColEdgeZEntry = _boxColSizeZEntry / 2;
        _boxColSizeZExit = new Vector3(0, 0, _exitCollider.size.x * _exitCollider.transform.localScale.x);
        _boxColEdgeZExit = _boxColSizeZExit / 2;

        _positionRightEntry = transform.position + this.transform.TransformDirection(_boxColEdgeXEntry);
        _positionLeftEntry = transform.position + this.transform.TransformDirection(-_boxColEdgeXEntry);
        _positionRightExit = _exitCollider.transform.position + _exitCollider.transform.TransformDirection(_boxColEdgeXExit);
        _positionLeftExit = _exitCollider.transform.position + _exitCollider.transform.TransformDirection(-_boxColEdgeXExit);

        _positionForwardEntry = transform.position + this.transform.TransformDirection(_boxColEdgeZEntry);
        _positionBackwardEntry = transform.position + this.transform.TransformDirection(-_boxColEdgeZEntry);
        _positionForwardExit = _exitCollider.transform.position + _exitCollider.transform.TransformDirection(_boxColEdgeZExit);
        _positionBackwardExit = _exitCollider.transform.position + _exitCollider.transform.TransformDirection(-_boxColEdgeZExit);
        //    -------------     // 
    }


    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out PlayerController player) && _onCooldown == false)
        {            
            StartCoroutine(StartCooldown());

            if (other.TryGetComponent(out SpeederGround speeder))
            {
                _autoJumpMaster.PlayerIsInTrigger = true;
                speeder.CurrentAutoJumpMaster = _autoJumpMaster;

                // (only do this if we want the perfect jump, and the jump is automatic)
                if (_autoJumpMaster.IsNormalPlayerJump == false && _autoJumpMaster.IsAutomatic == true)
                {
                    CreateJumpCurve(player);
                }

                // (only do this if it's allowed)
                if (_autoJumpMaster.IsAutomatic == true && speeder.IsGrounded == true)
                {
                    if (_autoJumpMaster.IsNormalPlayerJump == false)
                    {
                        // initiate the perfect jump   
                        _autoJumpMaster.DoPerfectJumpFakeGravity(player);                      
                    }
                    else
                    {
                        // activate normal jump (access speederGround script, call jump)
                        speeder.ForceJump();
                    }
                }
                else
                {
                    // extra logic present on speederGround
                }
            }
            else if (other.TryGetComponent(out SimpleCarController car))
            {
                // (only do this if we want the perfect jump)
                if (_autoJumpMaster.IsNormalPlayerJump == false)
                {
                    CreateJumpCurve(player);
                }

                // (only do this if it's allowed)  (will likely always be automatic, just requiring specific speed/angle on player)
                if (_autoJumpMaster.IsAutomatic == true)
                {                   
                    // if( (SPEED >= threshHold) && (looking in the right angle))   ====>   activate jump
                    if (_autoJumpMaster.RequiresSpeedCheck == true && car.Rigid.velocity.magnitude >= 4f || _autoJumpMaster.RequiresSpeedCheck == false)
                    {
                        if (_autoJumpMaster.IsNormalPlayerJump == false)
                        {
                            // initiate the perfect jump   
                            _autoJumpMaster.DoPerfectJumpFakeGravity(player);
                        }
                        else
                        {
                            // activate normal jump (access car script, call boost) (also adds a hop)
                            car.ForceBoost(_autoJumpMaster.AddHop, _autoJumpMaster.HopStrength);
                        }
                    }
                }
                else
                {
                    // nothing... always automatic
                }
            }
            else if (other.TryGetComponent(out EllaExploring exploring))
            {
                // (only do this if we want the perfect jump)
                if (_autoJumpMaster.IsNormalPlayerJump == false)
                {
                    CreateJumpCurve(player);
                }

                // (only do this if it's allowed)  will likely never be automated for car -> certain speed is needed
                if (_autoJumpMaster.IsAutomatic == true)
                {
                    if (_autoJumpMaster.IsNormalPlayerJump == false)
                    {
                        // initiate the perfect jump   
                        _autoJumpMaster.DoPerfectJumpFakeGravity(exploring);
                    }
                }
            }
        }
    }


    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out PlayerController player))
        {
            // only do this for the speeder
            if (player.TryGetComponent(out SpeederGround speeder))
            {
                _autoJumpMaster.PlayerIsInTrigger = false;
                speeder.CurrentAutoJumpMaster = null;
            }
        }
    }


    private IEnumerator StartCooldown()
    {
        _onCooldown = true;

        yield return new WaitForSeconds(1f);

        _onCooldown = false;
    }

    private void SetExitPosition(PlayerController player)
    {
        _percentageEntryX = Mathf.InverseLerp(_positionLeftEntry.x, _positionRightEntry.x, player.transform.position.x);
        _percentageEntryZ = Mathf.InverseLerp(_positionBackwardEntry.z, _positionForwardEntry.z, player.transform.position.z);
        _valuePosEntryX = Mathf.Lerp(_positionLeftEntry.x, _positionRightEntry.x, _percentageEntryX);
        _valuePosEntryZ = Mathf.Lerp(_positionBackwardEntry.z, _positionForwardEntry.z, _percentageEntryZ);      

        _valuePosExitX = Mathf.Lerp(_positionLeftExit.x, _positionRightExit.x, _percentageEntryX);
        _valuePosExitZ = Mathf.Lerp(_positionBackwardExit.z, _positionForwardExit.z, _percentageEntryZ);

        _entryPositionPlayer = new Vector3(_valuePosEntryX, _entryCollider.transform.position.y, _valuePosEntryZ);
        _exitPositionPlayer = new Vector3(_valuePosExitX, _exitCollider.transform.position.y, _valuePosExitZ);
    }

    private void SetExitPositionDefault()
    {
        _entryPositionPlayer = _entryCollider.transform.position;
        _exitPositionPlayer = _exitCollider.transform.position;
    }


    public void CreateJumpCurve(PlayerController player)
    {
        // find and set the exit position
        SetExitPosition(player);
        // create the arc for a perfect jump
        _autoJumpMaster.CalculateJumpWithFakeGravity(_entryPositionPlayer, _exitPositionPlayer);
    }


    // for drawing one when not playing
    public void CreateJumpCurveDefault()
    {
        // find and set the exit position
        SetExitPositionDefault();
        // create the arc for a perfect jump
        _autoJumpMaster.CalculateJumpWithFakeGravity(_entryPositionPlayer, _exitPositionPlayer);
    }
}
