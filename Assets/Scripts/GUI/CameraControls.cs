using UnityEngine;
using Cinemachine;
using Sandbox.Player;
using Rewired;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Transforms;


public struct CameraControlsComponent : IComponentData
{
    public float fov;
    public bool active;
    public LocalTransform localTransform;
    public float3 forward;
    public float3 right;
}

public class CameraControls : MonoBehaviour
{
    public Rewired.Player player;

    public int playerId = 0; // The Rewired player id of this character
    //public Camera overlayCam;
    //public Camera orthoCam;


    public CinemachineVirtualCamera vcam;
    public bool follow = true;
    private bool changeX, changeY;
    [Header("Free Look Rotation")] public CinemachineFreeLook freeLook;
    public CinemachineFreeLook freeLookCombat;
    public CinemachineInputAxisDriver xAxis;
    public CinemachineInputAxisDriver yAxis;
    public float minValueX = -360;
    public float maxValueX = 360;
    public float minHeight = 1;
    public float maxHeight = 24f;
    public float minRadius = 1;
    public float maxRadius = 120;
    public float xAxisValue;
    public float heightY;
    [HideInInspector]
    public float multiplierX = 90;
    public float multiplierY = 1f;

    //[Tooltip("multiply rig height by orbitRatio to set cam Radius")]
    [HideInInspector]
    public float orbitRatio = 2f;
    [HideInInspector]
    public float multiplierOrbit = 1f;
    private float startHeight;
    private float startRadius;
    float radiusValue;


    [SerializeField] PlayerWeaponAim playerWeaponAimReference;
 
    private void OnValidate()
    {
        xAxis.Validate();
        yAxis.Validate();
    }

    private void Reset()
    {
        xAxis = new CinemachineInputAxisDriver
        {
            multiplier = -10f,
            accelTime = 0.1f,
            decelTime = 0.1f,
            name = "Mouse X",
        };
        yAxis = new CinemachineInputAxisDriver
        {
            multiplier = 0.1f,
            accelTime = 0.1f,
            decelTime = 0.1f,
            name = "Mouse Y",
        };
    }


    void Start()
    {
        if (!ReInput.isReady) return;
        player = ReInput.players.GetPlayer(playerId);

        if (follow)
        {
            changeX = true;
            changeY = true;
        }

        startHeight = freeLook.m_Orbits[1].m_Height;
        startRadius = freeLook.m_Orbits[1].m_Radius;
        radiusValue = startRadius;
        heightY = startHeight;
        ChangeFov(false);
    }

    void LateUpdate()
    {
        //if (active == false) return;

        var controller = player.controllers.GetLastActiveController();
        var aimMode = false;
        if (playerWeaponAimReference)
        {
            aimMode = playerWeaponAimReference.aimMode;
        }

        if (controller == null || aimMode) return;

        var gamePad = controller.type == ControllerType.Joystick;
        var keyboard = controller.type == ControllerType.Keyboard;
        bool modifier = player.GetButton("RightBumper"); // get the "held" state of the button

        changeX = true;
        changeY = true;

        if (player.GetAxis("RightVertical") >= 1f)
        {
            if (follow)
            {
                changeX = false;
                changeY = true;
            }

            if (!modifier)
            {
                heightY -= Time.deltaTime * multiplierY;
            }
            else
            {
                radiusValue -= Time.deltaTime * multiplierY;
            }
            ChangeFov(modifier);
        }
        else if (player.GetAxis("RightVertical") <= -1f)
        {
            if (follow)
            {
                changeX = false;
                changeY = true;
            }

            if (!modifier)
            {
                heightY += Time.deltaTime * multiplierY;
            }
            else
            {
                radiusValue += Time.deltaTime * multiplierY;
            }

            ChangeFov(modifier);
        }

        if (player.GetAxis("RightHorizontal") >= 1)
        {
            xAxisValue += Time.deltaTime * multiplierX;
            if (follow)
            {
                changeX = true;
                changeY = false;
                xAxisValue = math.abs(xAxisValue);
            }

            ChangeFov(modifier);
        }
        else if (player.GetAxis("RightHorizontal") <= -1)
        {
            xAxisValue -= Time.deltaTime * multiplierX;
            if (follow)
            {
                changeX = true;
                changeY = false;
                xAxisValue = -math.abs(xAxisValue);
            }

            ChangeFov(modifier);
        }
    }


    public void ChangeFov(bool modifier)
    {
        if (freeLook)
        {

            if (changeX && !modifier)
            {
                xAxisValue = math.clamp(xAxisValue, minValueX, maxValueX);
                freeLook.m_XAxis.Value = xAxisValue;
            }

            if (changeY && !modifier)
            {
                heightY = math.clamp(heightY, minHeight, maxHeight);
                //freeLook.m_YAxis.Value = fovY;
                freeLook.m_Orbits[1].m_Height = heightY;
                //Debug.Log("fovy " + fovY);
                //Debug.Log("stht " + startHeight);

            }
            else if (changeY)
            {
                //freeLook.m_Orbits[1].m_Radius = freeLook.m_Orbits[1].m_Height * orbitRatio;
                radiusValue = math.clamp(radiusValue, minRadius, maxRadius);
                freeLook.m_Orbits[1].m_Radius = radiusValue;
            }


        }
    }
}