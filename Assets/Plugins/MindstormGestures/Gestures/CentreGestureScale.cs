/*
Unity3d-TUIO connects touch tracking from a TUIO to objects in Unity3d.

Copyright 2011 - Mindstorm Limited (reg. 05071596)

Author - Simon Lerpiniere

This file is part of Unity3d-TUIO.

Unity3d-TUIO is free software: you can redistribute it and/or modify
it under the terms of the GNU Lesser Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

Unity3d-TUIO is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Lesser Public License for more details.

You should have received a copy of the GNU Lesser Public License
along with Unity3d-TUIO.  If not, see <http://www.gnu.org/licenses/>.

If you have any questions regarding this library, or would like to purchase 
a commercial licence, please contact Mindstorm via www.mindstorm.com.
*/

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Mindstorm.Gesture;

public class CentreGestureDragScale : MonoBehaviour, IGestureHandler
{
	public float fixedDraggerHeight = 0f;
	public bool attachToParent = false;
	
	public GameObject dragger;
	
	public bool showDraggers = false;
	
	public int[] hitOnlyLayers = new int[1] { 0 };
	
	Dictionary<int, Joint> joints = new Dictionary<int, Joint>();
	Dictionary<int, GameObject> draggers = new Dictionary<int, GameObject>();
	Dictionary<int, Touch> touches = new Dictionary<int, Touch>();
	
	bool touchesChanged = false;
	
	Bounds b;
	Bounds oldB;
	
	Vector3 vel = Vector3.zero;
	
	Camera _targetCamera;
	
	Vector3 targetScale = Vector3.one;
	
	GameObject scaler = null;
	
	void Start()
	{
		_targetCamera = FindCamera();
		scaler = GameObject.CreatePrimitive(PrimitiveType.Sphere); //new GameObject("SCALER");
		scaler.collider.enabled = false;
		
		//targetScale = scaler.transform.localScale;
		targetScale = transform.localScale;
	}
	
	void changeBoundingBox()
	{
		bool boundsHasChanged = recalcBounds();
		if (!boundsHasChanged) return;
		
		// Only scale if we have the same number of touch points
		if (draggers.Count > 1)
		{
			float scaleVel = calcBoundsChange();
			targetScale = targetScale * scaleVel;
		}
	}
	
	void Update()
	{
		if (scaler.transform.localScale != targetScale)
		{
			//scaler.transform.position = calcScaleCentre();
			//transform.parent = scaler.transform;
				
			Vector3 curScale = Vector3.SmoothDamp(transform.localScale, targetScale, ref vel, 0.2f);
			if (Vector3.Distance(curScale, targetScale) < 0.01f) curScale = targetScale;
			//Vector3 pos = transform.position;
			//transform.position = calcScaleCentre();
			transform.localScale = new Vector3(curScale.x, transform.localScale.y, curScale.z);
			
			//transform.position = pos;
			
			
			//scaler.transform.localScale = curScale;
			
			//transform.parent = null;
		}
	}
	
	float calcBoundsChange()
	{
		float oldSize = Vector3.Distance(oldB.min, oldB.max);
		float newSize = Vector3.Distance(b.min, b.max);
		
		//Debug.DrawLine(oldB.min, oldB.max, Color.green);
		//Debug.DrawLine(b.min, b.max, Color.magenta);
		
		float velRatio = oldSize == 0f ? 1f : newSize/oldSize;
		return velRatio;
	}
	
	Vector3 calcScaleCentre()
	{
		var nonMovingIds = from Touch t in touches.Values
			where t.phase == TouchPhase.Stationary
			select t.fingerId;
		
		var nonMoving = from entry in draggers
			where nonMovingIds.Contains(entry.Key)
			select entry.Value;
		
		bool bfirst = true;
		Bounds nonB = new Bounds(Vector3.zero, Vector3.zero);
		foreach (GameObject dr in nonMoving)
		{
			if (bfirst)
			{
				nonB = new Bounds(dr.transform.position, Vector3.zero);
				bfirst = false;
			}
			else
			{
				//nonB.Encapsulate(dr.transform.position);
			}
		}
		
		Vector3 centre = nonMoving.Count() == 0 ? b.center : nonB.center;
		
		return centre;
	}
	
	bool recalcBounds()
	{
		bool boundsHasChanged = true;
		
		oldB = b;
		
		bool bfirst = true;
		foreach (GameObject dr in draggers.Values)
		{
			if (bfirst)
			{
				b = new Bounds(dr.transform.position, Vector3.zero);
				bfirst = false;
			}
			else
			{
				b.Encapsulate(dr.transform.position);
			}
		}
		
		if (touchesChanged || draggers.Count == 0)
		{
			oldB = b;
			boundsHasChanged = false;
		}
		return boundsHasChanged;
	}
	
	Ray getRay(Touch t)
	{
		Vector3 touchPoint = new Vector3(t.position.x, t.position.y, 0f);
		Ray targetRay = _targetCamera.ScreenPointToRay(touchPoint);
		return targetRay;
	}
	
	public void AddTouch(Touch t, RaycastHit hit)
	{
		touches.Add(t.fingerId, t);
		
		GameObject go = addDragger(hit.point, t, true);
		Rigidbody bod = attachToParent ? transform.parent.rigidbody : rigidbody;
		addJoint(bod, go, hit.point, t);
		
		touchesChanged = true;
	}
	
	public void RemoveTouch(Touch t)
	{
		touches.Remove(t.fingerId);
		
		removeJoint(t.fingerId);
		removeDragger(t);
		
		touchesChanged = true;
	}
	
	public void UpdateTouch(Touch t)
	{
		if (t.phase != TouchPhase.Moved) return;
		
		RaycastHit h = new RaycastHit();
		bool hasHit = (Physics.Raycast(getRay(t), out h, 100f, GetLayerMask(hitOnlyLayers)));	
		
		Vector3 hitPoint = h.point;
		if (h.collider.gameObject == gameObject)
				hitPoint = new Vector3(hitPoint.x, transform.position.y, hitPoint.z);
		
		updateDragger(hitPoint, t, hasHit);
	}
	
	void removeDragger(Touch t)
	{
		GameObject go = draggers[t.fingerId];
		draggers.Remove(t.fingerId);
		Destroy(go);
	}
	
	void removeJoint(int touchId)
	{
		if (!joints.ContainsKey(touchId)) return;
		
		Joint j = joints[touchId];
		j.connectedBody = null;
		
		Destroy(j);
		joints.Remove(touchId);
	}
	
	void initJoint(Joint joint, Rigidbody attachTo)
	{
		joint.connectedBody = attachTo;
	}
	
	void updateDragger(Vector3 hitPoint, Touch t, bool visible)
	{
		GameObject go = draggers[t.fingerId];
		float y = fixedDraggerHeight == 0 ? hitPoint.y : fixedDraggerHeight;
		go.transform.position = new Vector3(hitPoint.x, y, hitPoint.z);
		
		if (go.renderer != null) go.renderer.enabled = visible && showDraggers;
	}
	
	GameObject addDragger(Vector3 hitPoint, Touch t, bool visible)
	{
		GameObject go = (GameObject)Instantiate(dragger);
		float y = fixedDraggerHeight == 0 ? hitPoint.y : fixedDraggerHeight;
		go.transform.position = new Vector3(hitPoint.x, y, hitPoint.z);
		
		if (go.renderer != null) go.renderer.enabled = visible && showDraggers;
		
		draggers.Add(t.fingerId, go);
		return go;
	}
	
	Joint addJoint(Rigidbody attachTo, GameObject go, Vector3 hitPoint, Touch t)
	{
		Joint j = (Joint)go.GetComponent<Joint>();
		
		// Initial position of the joint is the centre of the body we pickup
		// It will move it in the next frame with the Drag
		go.transform.position = hitPoint;
		
		initJoint(j, attachTo);		
		joints.Add(t.fingerId, j);
		return j;
	}
	
	public void FinishNotification()
	{
		changeBoundingBox();
		touchesChanged = false;
	}
		
	Camera FindCamera ()
	{
		if (camera != null)
			return camera;
		else
			return Camera.main;
	}
		
	int GetLayerMask(int[] hitOnlyLayers)
	{
		if (hitOnlyLayers.Length == 0) 
			throw new System.ArgumentException("No layers in hitOnlyLayers array.  GetLayerMask requires at least one layer");
		
		var layerMask = 1 << hitOnlyLayers[0];
		for (int i = 1; i < hitOnlyLayers.Length; i++)
		{
			layerMask = layerMask | (1 << hitOnlyLayers[i]);
		}
		return layerMask;
	}
}