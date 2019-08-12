using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Drag and drop functionality for train cars
public class Drag_and_Drop_Train_Cars : MonoBehaviour
{
	//Whether the car is currently being dragged by the player
	public bool selected = false;

	//Whether the train car has been placed in the train
	public bool inTrain = false;

	//Starting position in train car list
	public Vector3 listPos;

	//Current position
	public Vector3 currentPos;

	//Slot the train car is assigned to
	public Collider2D currentCar = null;

	//Cargo and personnel slots within the car
	public List<GameObject> cargoSlots;

	//Container holding the list of train cars
	public Transform originalParent;

	//Master menu script
	private Train_Management_General masterMenu;

	//Whether the train car is currently visible to the player
	public bool visible = true;
	
	void Start(){
		masterMenu = GameObject.Find("Train_Management_Container").GetComponent<Train_Management_General>();

		listPos = transform.localPosition;

		listPos.x += 420;

		originalParent = transform.parent;

		//Identify and add slots to train car
		foreach (Transform child in gameObject.transform)
		{
			if (child != gameObject.transform)
			{
				cargoSlots.Add(child.gameObject);
				child.gameObject.SetActive(false);
			}
		}
	}

	//Pick up train car when clicked
	void OnMouseOver()
	{

		if (!visible) {
			return;
		}

		if (Input.GetMouseButtonDown(0))
		{
			selected = true;
			transform.SetParent(masterMenu.transform);
			currentPos = transform.position;
			GetComponent<Mouseover_Highlight_Train>().enabled = false;
			GetComponent<Mouseover_Highlight_Train>().RestoreColor();
		}
	}

	void Update()
	{
		if (selected || inTrain){
			visible = true;
		}
		else if (transform.position.y > 3.1f && !selected && !inTrain) {
			visible = false;
		}
		else if (transform.position.y < -2.5f && !selected && !inTrain){
			visible = false;
		}
		else if (!visible && transform.position.y < 3.1f && transform.position.y > -2.5f){
			visible = true;
		}

		//Follow Mouse
		if (selected)
		{
			Vector2 cursorPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
			Vector3 newPos = new Vector3(cursorPos.x, cursorPos.y, transform.position.z);
			transform.position = newPos;
		}


		if (Input.GetMouseButtonUp(0) && selected)
		{
			GetComponent<Mouseover_Highlight_Train>().enabled = true;

			Collider2D tempNewCar = null;	

			selected = false;

			//Check if car is dropped in a slot
			GameObject[] carSlots = GameObject.FindGameObjectsWithTag("Train_Car_Slot");

			Collider2D collider = gameObject.GetComponent<Collider2D>();

			foreach (GameObject slot in carSlots)
			{
				Collider2D slotCollider = slot.GetComponent<Collider2D>();
				if (collider.bounds.Intersects(slotCollider.bounds))
				{
					inTrain = true;
					currentPos = slotCollider.bounds.center;
					currentPos.z = transform.position.z;
					tempNewCar = slotCollider;
					break;
				}
				else {
					inTrain = false;
				}
			}

			//If not dropped in a slot, return to list, otherwise place in slot
			if (!inTrain)
			{
				if (masterMenu.activeMenu == 0)
				{
					transform.SetParent(originalParent, true);

					transform.localPosition = listPos;
					currentPos = listPos;

					for (int i = 0; i < cargoSlots.Count; i++)
					{
						cargoSlots[i].SetActive(false);
					}
				}
				else {
					transform.position = currentPos;
					return;
				}
			}
			else
			{
				//Check if slot is already filled
				transform.position = currentPos;

				for (int i = 0; i < cargoSlots.Count; i++)
				{
					cargoSlots[i].SetActive(true);
				}

				GameObject[] trainCars = GameObject.FindGameObjectsWithTag("Menu_Train_Car");

				foreach (GameObject car in trainCars)
				{
					if (car == gameObject)
					{
						continue;
					}

					Collider2D carCollider = car.GetComponent<Collider2D>();

					//If slot is filled swap places with old car
					if (collider.bounds.Intersects(carCollider.bounds) && tempNewCar.bounds.Intersects(carCollider.bounds))
					{
						car.GetComponent<Drag_and_Drop_Train_Cars>().UpdatePos(currentCar);
					}
				}
			}

			currentCar = tempNewCar;
			if (tempNewCar != null)
			{
				transform.SetParent(tempNewCar.gameObject.transform, true);
			}
		}
	}

	public void UpdatePos(Collider2D collider){

		//Return to list
		if (collider == null){
			inTrain = false;
			currentCar = null;

			transform.SetParent(originalParent, true);

			transform.localPosition = listPos;
			currentPos = listPos;

			for (int i = 0; i < cargoSlots.Count; i++){
				cargoSlots[i].SetActive(false);
			}
		}
		//Place in train
		else {
			inTrain = true;
			currentCar = collider;
			currentPos = currentCar.bounds.center;
			currentPos.z = transform.position.z;
			transform.position = currentPos;
			transform.SetParent(currentCar.gameObject.transform, true);

			for (int i = 0; i < cargoSlots.Count; i++)
			{
				cargoSlots[i].SetActive(true);
			}
		}
	}
}
