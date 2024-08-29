using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public enum OffMeshLinkMoveMethod
{
    Teleport,
    NormalSpeed,
    Parabola,
    Curve

}

[RequireComponent(typeof(NavMeshAgent))]
public class AgentLinkMover : MonoBehaviour
{
    //public EnemyMove enemyMove;
    public OffMeshLinkMoveMethod method = OffMeshLinkMoveMethod.Parabola;
    public AnimationCurve curve = new AnimationCurve();
    NavMeshAgent agent;
    public float height = 2.0f;
    public float duration = .5f;//change for greater hang time, varying enemies. further between start and end may lead to duration differences
    private static readonly int JumpState = Animator.StringToHash("JumpState");
    private float normalizedTime = 0;
    public bool isAgentNavigatingLink = false;
    OffMeshLinkData data;
    private Vector3 startPos;
    private Vector3 endPos;
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
     
    }

  

    private void Update()
    {
        if (agent.isOnOffMeshLink && !isAgentNavigatingLink)
        {
            isAgentNavigatingLink = true;
            data = agent.currentOffMeshLinkData;
            startPos = agent.transform.position;
            endPos = data.endPos + Vector3.up * (agent.baseOffset);
        }


        if (isAgentNavigatingLink)
        {
            if (method == OffMeshLinkMoveMethod.Parabola)
            {
                Parabola();
            }
            else if (method == OffMeshLinkMoveMethod.Curve)
            {
                Curve();
            }
            else if (method == OffMeshLinkMoveMethod.NormalSpeed)
            {
                NormalSpeed();
            }
            else if (method == OffMeshLinkMoveMethod.Teleport)
            {
                Teleport();
            }
        }


    }
    
    void NormalSpeed()
    {
        if (agent.transform.position != endPos)
        {
            agent.transform.position = Vector3.MoveTowards(agent.transform.position, endPos, agent.speed * Time.deltaTime);
        }
        else
        {
            normalizedTime = 0;
            isAgentNavigatingLink = false;
            agent.CompleteOffMeshLink();
        }
    }

    void Teleport()
    {
        if (agent.transform.position != endPos)
        {
            agent.transform.position = endPos;
        }
        else
        {
            normalizedTime = 0;
            isAgentNavigatingLink = false;
            agent.CompleteOffMeshLink();
        }
    }

    void Parabola()
    {
        //var endPos = data.endPos + Vector3.up * 1;
        if(normalizedTime < 1.0f)
        {
            var yOffset = height * 4.0f * (normalizedTime - normalizedTime * normalizedTime);
            Transform transform1;
            (transform1 = agent.transform).position = Vector3.Lerp(startPos, endPos, normalizedTime) + yOffset * Vector3.up;
            agent.destination = transform1.position;
            normalizedTime += Time.deltaTime / duration;
            //Debug.Log("AGENT PARA NORM TIME " + normalizedTime);

        }
        else 
        {
            normalizedTime = 0;
            isAgentNavigatingLink = false;
            agent.CompleteOffMeshLink();
        }




    }
    
    void Curve()
    {
        //var endPos = data.endPos + Vector3.up * 1;
        if(normalizedTime < 1.0f)
        {
            var yOffset = curve.Evaluate(normalizedTime);
            Transform transform1;
            (transform1 = agent.transform).position = Vector3.Lerp(startPos, endPos, normalizedTime) + yOffset * Vector3.up;
            agent.destination = transform1.position;
            normalizedTime += Time.deltaTime / duration;

        }
        else 
        {
            normalizedTime = 0;
            isAgentNavigatingLink = false;
            agent.CompleteOffMeshLink();
        }




    }




    









}

