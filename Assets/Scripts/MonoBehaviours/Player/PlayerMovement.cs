using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField]
    Animator _animator;

    [SerializeField]
    NavMeshAgent _agent;

    [SerializeField]
    float _inputHoldDelay = .5f;

    [SerializeField]
    float _turnSpeedThreshold = .5f;

    [SerializeField]
    float _speedDampTime = .1f;

    [SerializeField]
    float _slowingSpeed = .175f;

    [SerializeField]
    float _turnSmoothing = 15f;

    const float _stopDistanceProportion = .1f;
    const float _navMeshSampleDistance = 4f;

    readonly int _hashSpeedParam = Animator.StringToHash("Speed");
    readonly int _hashLocomotionTag = Animator.StringToHash("Locomotion");

    WaitForSeconds _inputHoldWait;
    Vector3 _destinationPosition;
    Interactable _currentInteractable;
    bool _isLocked = false;

    public void OnGroundClick(BaseEventData data)
    {
        if (_isLocked)
            return;

        _currentInteractable = null;

        PointerEventData pointerData = (PointerEventData) data;
        NavMeshHit meshHit;
        if (NavMesh.SamplePosition(pointerData.pointerCurrentRaycast.worldPosition, out meshHit, _navMeshSampleDistance, NavMesh.AllAreas))
        {
            _destinationPosition = meshHit.position;
        }
        else
        {
            _destinationPosition = pointerData.pointerCurrentRaycast.worldPosition;
        }

        _agent.SetDestination(_destinationPosition);
        _agent.isStopped = false;
    }

    public void OnInteractableClick(Interactable clickedInteractable)
    {
        if (_isLocked)
            return;

        _currentInteractable = clickedInteractable;
        _destinationPosition = clickedInteractable.interactionLocation.position;

        _agent.SetDestination(_destinationPosition);
        _agent.isStopped = false;
    }

    void Start()
    {
        _agent.updateRotation = false;
        _inputHoldWait = new WaitForSeconds(_inputHoldDelay);
        _destinationPosition = transform.position;
    }

    void Update()
    {
        if (_agent.pathPending)
            return;

        // FIXME: Looks like a NavMesh has it's own slowing down, so that _agent.desiredVelocity.magnitude,
        //        but very often it's value jumps back to 2f for a single frame. As a result
        //        we have strange animation glitches at the end of the player's movement.
        float speed = _agent.desiredVelocity.magnitude;
        if (_agent.remainingDistance <= _agent.stoppingDistance * _stopDistanceProportion)
        {
            Stopping(out speed);
        }
        else if (_agent.remainingDistance <= _agent.stoppingDistance)
        {
            Slowing(out speed, _agent.remainingDistance);
        }
        else if (speed > _turnSpeedThreshold)
        {
            Moving();
        }

        _animator.SetFloat(_hashSpeedParam, speed, _speedDampTime, Time.deltaTime);
    }

    void OnAnimatorMove()
    {
        _agent.velocity = _animator.deltaPosition / Time.deltaTime;
    }

    void Stopping(out float speed)
    {
        _agent.isStopped = true;
        transform.position = _destinationPosition;
        speed = 0f;

        if (_currentInteractable)
        {
            transform.rotation = _currentInteractable.interactionLocation.rotation;
            _currentInteractable.Interact();
            _currentInteractable = null;

            StartCoroutine(WaitForInteraction());
        }
    }

    void Slowing(out float speed, float distanceToDestination)
    {
        _agent.isStopped = true;
        transform.position = Vector3.MoveTowards(transform.position, _destinationPosition, _slowingSpeed * Time.deltaTime);
        float proportionalDistance = 1f - (distanceToDestination / _agent.stoppingDistance);
        speed = Mathf.Lerp(_slowingSpeed, 0f, proportionalDistance);

        Quaternion targetRotation = _currentInteractable ? _currentInteractable.interactionLocation.rotation : transform.rotation;
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, proportionalDistance);
    }

    void Moving()
    {
        Quaternion targetRotation = Quaternion.LookRotation(_agent.desiredVelocity);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, _turnSmoothing * Time.deltaTime);
    }

    IEnumerator WaitForInteraction()
    {
        _isLocked = true;

        yield return _inputHoldWait;

        while (_animator.GetCurrentAnimatorStateInfo(0).tagHash != _hashLocomotionTag)
        {
            yield return null;
        }

        _isLocked = false;
    }
}
