using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AutoJumpMaster : MonoBehaviour
{
    [Header("only values you should adjust")]
    [SerializeField]
    private float _fakeGravity; 

    /// <summary>
    /// player input VS no input.
    /// </summary>
    public bool IsAutomatic = true;
    [HideInInspector]
    public bool PlayerIsInTrigger;

    /// <summary>
    /// normal player jump VS pre-calculated movement.
    /// </summary>
    public bool IsNormalPlayerJump = false;

    [Header("values for Glitch")]
    public bool AddHop = true;
    public float HopStrength = 10;
    public bool RequiresSpeedCheck = false;

    [HideInInspector]
    public float LaunchSpeed = 1f; 

    [Header("Components &|| Children")]
    [SerializeField]
    private GameObject _start;
    [SerializeField]
    private GameObject _end;
    [SerializeField]
    private Transform _transformDirectionXZ;
    [SerializeField]
    private Transform _transformDirectionY;
    [SerializeField]
    private AutoJumpEntryTrigger _entryTrigger;

    [Header("Swap camera if present")]
    [SerializeField]
    private SwapCamera _swapCamera;

    [Header("Zoom params")]
    [SerializeField] private float _zoomDuration = 3f;
    [SerializeField, Range(0.01f, 0.5f)] private float _zoomFactor = 0.02f;
    [SerializeField, Range(1f, 4f)] private float _zoomLimit = 2.5f;

    [Header("Visualizations Arc")]
    [SerializeField]
    private int _iterations = 20;
    [SerializeField]
    private LineRenderer _lineRenderer;

    private float _currentAngle;
    private bool _isOnCooldown;
    [SerializeField]
    private float _currentTimeOfFlight;

    [Header("Trigger Visualisations")]
    [SerializeField]
    private List<MeshRenderer> _allMeshrenderers = new List<MeshRenderer>();


    private void Start()
    {
        if (_swapCamera != null)
        {
            _swapCamera.gameObject.SetActive(false);
        }
    }


    // called from trigger OR player, it depends
    public void DoPerfectJumpFakeGravity(PlayerController player)
    {
        if (_isOnCooldown == false)
        {
            // Exploring
            if (player.TryGetComponent(out EllaExploring exploring))
            {
                // start cooldown
                StartCoroutine(JumpPadCooldown());

                // enable the SwapCamera if present
                ActivateSwapCamera();
                // disable the character component + adjust gravity
                exploring.JumpPadCCToggleFakeGravity(_currentTimeOfFlight, _fakeGravity);
                // set the rigidbody velocity
                exploring.Rigid.velocity = _transformDirectionY.up * LaunchSpeed;
                // set the playier in proper direction and animation
                exploring.JumpPadAnimater(_transformDirectionXZ);
                // add zoom
                ServiceLocator.Instance.GetService<VirtualCameraManagerExploring>().ZoomOutCameraDistance(_zoomDuration, _zoomFactor, _zoomLimit);
            }
            else if (player.TryGetComponent(out SimpleCarController car)) // Glitch
            {
                // disable the character component + adjust gravity
                car.AutoJumpToggleFakeGravity(_currentTimeOfFlight, _fakeGravity);
                // set the rigidbody velocity
                car.Rigid.velocity = _transformDirectionY.up * LaunchSpeed;
            }
            else if (player.TryGetComponent(out SpeederGround speeder)) // Moonscooter
            {
                // disable the character component + adjust gravity
                speeder.AutoJumpCCToggleFakeGravity(_currentTimeOfFlight, _fakeGravity);
                // set the rigidbody velocity
                speeder.Rigid.velocity = _transformDirectionY.up * LaunchSpeed;
            }
        }
    }

    public void CreatePerfectJumpCurve(PlayerController player)
    {
        _entryTrigger.CreateJumpCurve(player);
    }
    // we use this to draw a curve when not playing 
    public void CreatePerfectJumpCurveDefault()
    {
        _entryTrigger.CreateJumpCurveDefault();
    }


    private void ActivateSwapCamera()
    {
        if (_swapCamera != null)
        {
            StartCoroutine(ToggleSwapCamera());
        }
    }
    private IEnumerator ToggleSwapCamera()
    {
        _swapCamera.gameObject.SetActive(true);

        yield return new WaitForSeconds(_currentTimeOfFlight);

        _swapCamera.gameObject.SetActive(false);
    }
    private IEnumerator JumpPadCooldown()
    {
        _isOnCooldown = true;

        yield return new WaitForSeconds(3f);

        _isOnCooldown = false;
    }


    public void CalculateJumpWithFakeGravity(Vector3 entryPos, Vector3 exitPos, bool useLowAngle = false)
    {
        if (_fakeGravity == 0)
        {
            Debug.Log("Gravity set to 0 -> stopping here. Perhaps you forgot to assign me a value?");
            return;
        }

        if (_fakeGravity <= 0)
        {
            _fakeGravity *= (-1);
        }

        _start.transform.position = entryPos;
        _end.transform.position = exitPos;

        Vector3 direction = _start.transform.position - _end.transform.position;
        float yOffset = -direction.y;
        direction = ArcMath.ProjectVectorOnPlane(Vector3.up, direction);
        float distance = direction.magnitude;

        // 2 angles will be calculated for a possible launch
        float angle0, angle1;
        // initial check whether the target is in range
        bool targetInRange = ArcMath.LaunchAngle(LaunchSpeed, distance, yOffset, _fakeGravity, out angle0, out angle1);
        while (targetInRange == false)
        {
            LaunchSpeed += 1;

            targetInRange = ArcMath.LaunchAngle(LaunchSpeed, distance, yOffset, _fakeGravity, out angle0, out angle1);
        }

        if (targetInRange == true)
        {
            if (useLowAngle == true)
            {
                _currentAngle = angle1;
            }
            else
            {
                _currentAngle = angle0;
            }
        }
        else
        {
            Debug.Log("target not in range for -> " + this.gameObject.name);
            return;
        }

        // set angle for propulsion
        _transformDirectionXZ.rotation = Quaternion.LookRotation(-direction) * Quaternion.Euler(-90, -90, 0);
        var propulsionAngle = (_currentAngle * Mathf.Rad2Deg);
        _transformDirectionY.localRotation = Quaternion.Euler(90, 90, 0) * Quaternion.AngleAxis(propulsionAngle, Vector3.forward);

        // visualizes the arc (needs to use the gravity set on the player)
        UpdateArc(LaunchSpeed, distance, _fakeGravity, _currentAngle, direction, targetInRange);

        // calculates time it would take for the launched object to reach the target
        _currentTimeOfFlight = ArcMath.TimeOfFlight(LaunchSpeed, _currentAngle, -yOffset, _fakeGravity);
    }
    public void CalculateJumpWithPlayerGravity(Vector3 entryPos, Vector3 exitPos, float playerGravity, bool useLowAngle = false)
    {
        if (playerGravity == 0)
        {
            Debug.Log("Gravity set to 0 -> stopping here. Perhaps you forgot to assign me a value?");
            return;
        }

        if (playerGravity <= 0)
        {
            playerGravity *= (-1);
        }

        _start.transform.position = entryPos;
        _end.transform.position = exitPos;

        Vector3 direction = _start.transform.position - _end.transform.position;
        float yOffset = -direction.y;
        direction = ArcMath.ProjectVectorOnPlane(Vector3.up, direction);
        float distance = direction.magnitude;

        // 2 angles will be calculated for a possible launch
        float angle0, angle1;
        // initial check whether the target is in range
        bool targetInRange = ArcMath.LaunchAngle(LaunchSpeed, distance, yOffset, playerGravity, out angle0, out angle1);
        while (targetInRange == false)
        {
            LaunchSpeed += 1;

            targetInRange = ArcMath.LaunchAngle(LaunchSpeed, distance, yOffset, playerGravity, out angle0, out angle1);
        }

        if (targetInRange == true)
        {
            if (useLowAngle == true)
            {
                _currentAngle = angle1;
            }
            else
            {
                _currentAngle = angle0;
            }
        }
        else
        {
            Debug.Log("target not in range for -> " + this.gameObject.name);
            return;
        }

        // set angle for propulsion
        _transformDirectionXZ.rotation = Quaternion.LookRotation(-direction) * Quaternion.Euler(-90, -90, 0);
        var propulsionAngle = (_currentAngle * Mathf.Rad2Deg);
        _transformDirectionY.localRotation = Quaternion.Euler(90, 90, 0) * Quaternion.AngleAxis(propulsionAngle, Vector3.forward);

        // visualizes the arc (needs to use the gravity set on the player)
        UpdateArc(LaunchSpeed, distance, playerGravity, _currentAngle, direction, targetInRange);

        // calculates time it would take for the launched object to reach the target
        _currentTimeOfFlight = ArcMath.TimeOfFlight(LaunchSpeed, _currentAngle, -yOffset, playerGravity);
    }
    public void ToggleAutoJumpVisuals(bool showThem = true)
    {
        // hide/show all meshrenderers regarding the jump
        for (int i = 0; i < _allMeshrenderers.Count; i++)
        {
            _allMeshrenderers[i].enabled = showThem;
        }

        // hide/show the arc
        _lineRenderer.gameObject.SetActive(showThem);
    }


   

    /// <summary>
    /// This will visualize the arc using the linerenderer component
    /// </summary>
    /// <param name="speed"></param>
    /// <param name="distance"></param>
    /// <param name="gravity"></param>
    /// <param name="angle"></param>
    /// <param name="direction"></param>
    /// <param name="valid"></param>
    private void UpdateArc(float speed, float distance, float gravity, float angle, Vector3 direction, bool valid)
    {
        Vector2[] arcPoints = ArcMath.ProjectileArcPoints(_iterations, speed, distance, gravity, angle);
        Vector3[] points3d = new Vector3[arcPoints.Length];

        for (int i = 0; i < arcPoints.Length; i++)
        {
            points3d[i] = new Vector3(0, arcPoints[i].y, arcPoints[i].x);
        }

        _lineRenderer.positionCount = arcPoints.Length;
        _lineRenderer.SetPositions(points3d);

        _lineRenderer.transform.rotation = Quaternion.LookRotation(-direction);
    }
}
