using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//The Basic Werewolf will randomly select a train car to spawn at and then select one of four spawn points on that car.
//It will prefer a breached spawn point, but if none are available, one will randomly be selected.
//After breaching the spawn point, it will enter the train and select the nearest friendly to attack.
//If there are no friendlies on the train, it will attack destructibles. 
//Once all destructibles and friendlies in its car are destroyed or dead, it will move forward through the train.
public class Basic_Werewolf : MonoBehaviour
{
	//Contains the werewolf's stats
	public Basic enemy;
	
	//Werewolf's collsion box
	private Collider2D myCollider;

	//Collider when walking left or right
	public Collider2D horizCollider;

	//Collider when walking forward or backward
	public Collider2D vertCollider;

	//Sprite Animator
	public Animator myAnimator;

	//The werewolf's current target
	public GameObject target;

	//Used to determine attack intervals
	private float startTime;

	//The car the werewolf is currently in
	public GameObject currentCar;

	//Spawn Location - The werewolf's spawn point
	public GameObject spawnLocation;

	//Werewolf's max HP
	private int maxHP;

	//A random point inside the car the werewolf will walk to
	private Vector2 wanderPoint;

	//Whether the current target is a destructible or friendly
	private int targetType = 1;

	//Whether the werewolf has breached the car
	public bool hasEnteredCar = false;

	//Whether the werewolf is currently in combat
	public bool inCombat = false;

	public bool isWandering = false;

	//Whether the werewolf is currently in the process of moving between train cars
	public bool movingCars = false;

	//Attack and Movement Speed Multipliers
	private float attackMult = 0.5f;
	private float speedMult = 0.75f;

	public SpriteRenderer myRenderer;

	// Start is called before the first frame update
	void Start()
    {
		enemy = new Basic();

		maxHP = enemy.hp;

		//Set current car to the car the enemy spawned at
		currentCar = spawnLocation.transform.parent.gameObject.transform.parent.gameObject;

		transform.SetParent(currentCar.transform, true);

		currentCar.GetComponent<Train_Car_Info>().trainCar.numWerewolves++;

		startTime = Time.time;

		myCollider = vertCollider;

		wanderPoint = WanderAround(currentCar.GetComponent<Collider2D>().bounds);

		//Check if spawn location has been breached,
		//if not set the breach point as the current target
		if (spawnLocation.GetComponent<Destructible_General>().hp > 0){
			inCombat = true;
			targetType = 1;
			target = spawnLocation;
		}
    }

    // Update is called once per frame
    void Update()
    {
		myAnimator.SetBool("attacking", false);

		//Die if at 0 health
		if (enemy.hp <= 0)
		{
			currentCar.GetComponent<Train_Car_Info>().trainCar.numWerewolves--;
			Destroy(gameObject);
		}

		//Check whether the werewolf is in combat
		if (inCombat)
		{
			//Make sure target is still in attack range
			if (target != null && (myCollider.bounds.Intersects(target.GetComponent<Collider2D>().bounds) || !hasEnteredCar))
			{
				//In Combat, Attack Target

				float deltaT = Time.time - startTime;

				//Attack on intervals based on enemy attack speed
				if (deltaT >= (enemy.speed * attackMult))
				{
					int attackChance = Random.Range(1, 11);

					//Check if attack hits
					if (attackChance <= enemy.attack)
					{
						if (target != null)
						{
							myAnimator.SetBool("attacking", true);
							target.BroadcastMessage("ChangeHP", -enemy.damage);
						}
					}

					startTime = Time.time;
				}

				//Check if target has been defeated
				//If target is a friendly, check if it has been killed
				//If the target is a destructable, check if its HP reached zero
				if (target == null || (targetType == 1 && target.GetComponent<Destructible_General>().hp <= 0))
				{
					target = null;
					inCombat = false;
					startTime = Time.time;

					SetAnimation(gameObject.transform.position, gameObject.transform.position, false);
				}
				else
				{
					SetAnimation(gameObject.transform.position, target.transform.position, true);
				}
			}
			else {
				//Target is no longer in range
				target = null;
				inCombat = false;
				startTime = Time.time;

				SetAnimation(gameObject.transform.position, gameObject.transform.position, false);
			}
		}
		else if (!inCombat && !hasEnteredCar && !movingCars) {
			//Upon breaching the train car, enter the car

			Collider2D trainColl = currentCar.GetComponent<Collider2D>();
			
			//Check if the werewolf is completely inside the car
			if (IsInside(trainColl)){
				hasEnteredCar = true;
			}
			else
			{
				//If not yet inside car, continue moving into car

				float deltaT = Time.time - startTime;

				Vector2 pos = gameObject.transform.position;
				
				//Check whether the werewolf needs to move up or down
				if (gameObject.transform.position.y > 0)
				{
					pos.y -= enemy.speed * deltaT * speedMult;
				}
				else
				{
					pos.y += enemy.speed * deltaT * speedMult;
				}

				SetAnimation(gameObject.transform.position, pos, false);

				gameObject.transform.position = pos;

				startTime = Time.time;
			}
		}
		else if (hasEnteredCar && !inCombat && !movingCars){
			//If the enemy is inside the train car, but not currently in combat, select a new target

			//Find a friendly to attack
			target = FindNearestFriendly();

			//If there are no friendlies on the car, select a destructible to attack
			if (target == null) {
				target = FindNearestDestructible();
			}

			if (target != null && isWandering)
			{
				isWandering = false;
				startTime = Time.time;
			}

			//Check whether there is a target inside the current car
			if (target != null)
			{
				//Move to target
				float deltaT = Time.time - startTime;

				Vector2 newPos = Vector2.MoveTowards(gameObject.transform.position, target.transform.position, enemy.speed * deltaT * speedMult);
				Bounds carBounds = currentCar.GetComponent<Collider2D>().bounds;

				//Make sure werewolf doesn't move outside the train car's boundaries
				newPos = PosInTrainCar(newPos, carBounds);

				SetAnimation(gameObject.transform.position, newPos, false);

				gameObject.transform.position = newPos;

				//Initiate combat on collision
				if (myCollider.bounds.Intersects(target.GetComponent<Collider2D>().bounds))
				{
					inCombat = true;
				}

				startTime = Time.time;
			}
			else
			{
				//If no target is selected, and there is no front car to move to, move to a random point within the car
				if (currentCar.GetComponent<Train_Car_Info>().trainCar.front == null)
				{
					isWandering = true;

					//Check if the random point exists or if it has been reached
					if (wanderPoint == null || Vector2.Distance(gameObject.transform.position, wanderPoint) < .001f)
					{
						//Select a new point after a short pause
						float currentTime = Time.time;
						float deltaT = currentTime - startTime;

						if (deltaT >= 3.0f)
						{
							wanderPoint = WanderAround(currentCar.GetComponent<Collider2D>().bounds);
							startTime = Time.time;
						}

						SetAnimation(gameObject.transform.position, gameObject.transform.position, false);
					}
					else
					{
						//Move towards random point
						float deltaT = Time.time - startTime;

						Vector2 newPos = Vector2.MoveTowards(gameObject.transform.position, wanderPoint, enemy.speed * deltaT * speedMult);
						newPos = PosInTrainCar(newPos, currentCar.GetComponent<Collider2D>().bounds);

						SetAnimation(gameObject.transform.position, newPos, false);

						gameObject.transform.position = newPos;

						startTime = Time.time;
					}
				}
				else {
					//Move forwards one car
					float deltaT = Time.time - startTime;

					GameObject exitPoint = currentCar.GetComponent<Train_Car_Info>().frontDoor;

					Vector2 newPos = Vector2.MoveTowards(gameObject.transform.position, exitPoint.transform.position, enemy.speed * deltaT * speedMult);

					startTime = Time.time;

					Bounds carBounds = currentCar.GetComponent<Collider2D>().bounds;

					newPos = PosInTrainCar(newPos, carBounds);

					SetAnimation(gameObject.transform.position, newPos, false);

					gameObject.transform.position = newPos;

					//HP of the front door of the car
					int hp = currentCar.GetComponent<Train_Car_Info>().frontDoorArea.GetComponent<Destructible_General>().hp;

					//Check if front door has been reached and if it has been destroyed
					if (Vector2.Distance(gameObject.transform.position, exitPoint.transform.position) <= 1.0f && hp <= 0){
						//Initiate process for moving train cars
						if (currentCar.GetComponent<Train_Car_Behavior>().isConnected)
						{
							movingCars = true;

							currentCar.GetComponent<Train_Car_Info>().trainCar.numWerewolves--;

							currentCar = currentCar.GetComponent<Train_Car_Info>().frontCar.GetComponent<Train_Car_Info>().trainCar.carObject;

							transform.SetParent(currentCar.transform, true);

							currentCar.GetComponent<Train_Car_Info>().trainCar.numWerewolves++;
							//Set new front and rear cars
							Train_Car front = currentCar.GetComponent<Train_Car_Info>().trainCar.front;
							if (front != null)
							{
								currentCar.GetComponent<Train_Car_Info>().frontCar = front.carObject;
							}
							else
							{
								currentCar.GetComponent<Train_Car_Info>().frontCar = null;
							}

							Train_Car rear = currentCar.GetComponent<Train_Car_Info>().trainCar.behind;
							if (rear != null)
							{
								currentCar.GetComponent<Train_Car_Info>().rearCar = rear.carObject;
							}
							else
							{
								currentCar.GetComponent<Train_Car_Info>().rearCar = null;
							}

							//Set new Wander Point within the new car
							wanderPoint = WanderAround(currentCar.GetComponent<Collider2D>().bounds);
						}
					}
				}
			}
		}
		else if (movingCars){
			//Move to new car

			//Check if entrance to new car has been destroyed
			if (currentCar.GetComponent<Train_Car_Info>().backDoorArea.GetComponent<Destructible_General>().hp <= 0)
			{
				//If entrance has been destroyed, enter car

				Collider2D trainColl = currentCar.GetComponent<Collider2D>();

				//Check if the enemy is entirely inside the train car
				if (IsInside(trainColl))
				{
					movingCars = false;
					hasEnteredCar = true;
				}
				else
				{
					//If not, continue moving into train car
					float deltaT = Time.time - startTime;

					Vector2 pos = gameObject.transform.position;
					
					pos.x += enemy.speed * deltaT * speedMult;

					SetAnimation(gameObject.transform.position, pos, false);

					gameObject.transform.position = pos;
				}

				startTime = Time.time;
			}
			else
			{
				//If the entrance has not been destroyed, move towards it and then attack it
				float deltaT = Time.time - startTime;

				GameObject entrancePoint = currentCar.GetComponent<Train_Car_Info>().backDoor;

				if (Vector2.Distance(gameObject.transform.position, entrancePoint.transform.position) > 0.75f)
				{
					//Move to target

					Vector2 newPos = Vector2.MoveTowards(gameObject.transform.position, entrancePoint.transform.position, enemy.speed * deltaT * speedMult);

					SetAnimation(gameObject.transform.position, newPos, false);

					gameObject.transform.position = newPos;
				}
				else
				{
					//Target reached, begin combat
					inCombat = true;
					hasEnteredCar = false;
					target = currentCar.GetComponent<Train_Car_Info>().backDoorArea;
				}

				startTime = Time.time;
			}
		}
    }

    //Select the nearest friendly
	public GameObject FindNearestFriendly()
	{
		//An array of all friendly NPCs on the train
		GameObject[] friendlies;
		friendlies = GameObject.FindGameObjectsWithTag("Friendly");
		
		GameObject closest = null;
		float distance = Mathf.Infinity;
		Vector3 position = transform.position;
		
		foreach (GameObject friendly in friendlies)
		{
			//Make sure current friendly is in the same car as the werewolf
			if (currentCar.GetComponent<Collider2D>().bounds.Intersects(friendly.GetComponent<Collider2D>().bounds))
			{
				Vector3 diff = friendly.transform.position - position;
				float curDistance = diff.sqrMagnitude;
				
				//Check if friendly is closer than currently selected friendly
				if (curDistance < distance) { 
					closest = friendly;
					distance = curDistance;
				}
			}
		}

		//Target is a friendly
		targetType = 0;

		return closest;
	}

	//Select nearet destructible
	public GameObject FindNearestDestructible()
	{
		//An array of all destructibles on the train
		GameObject[] destructibles;
		destructibles = GameObject.FindGameObjectsWithTag("Destructible");

		GameObject closest = null;
		float distance = Mathf.Infinity;
		Vector3 position = transform.position;

		foreach (GameObject destructible in destructibles)
		{
			//Make sure current destructible is in the same car as the werewolf
			if (currentCar.GetComponent<Collider2D>().bounds.Intersects(destructible.GetComponent<Collider2D>().bounds))
			{
				Vector3 diff = destructible.transform.position - position;
				float curDistance = diff.sqrMagnitude;

				//Make sure the destructible has not been destroyed
				if (destructible.GetComponent<Destructible_General>().hp > 0)
				{
					//Check if destructible is closer than the already selected destructible
					if (curDistance < distance)
					{
						closest = destructible;
						distance = curDistance;
					}
				}
			}
		}

		//Target is a destructible
		targetType = 1;

		return closest;
	}

	//Change the werewolf's HP
	public void ChangeHP(int delta)
	{
		//Make sure the werewolf isn't overhealed
		if (enemy.hp + delta >= maxHP)
		{
			enemy.hp = maxHP;
		}
		else
		{
			enemy.hp += delta;
		}

		if (delta > 0){
			StartCoroutine("IndicateHeal");
		}
		else {
			StartCoroutine("IndicateDamage");
		}
	}

	//Find a random point in the train car to wander to
	public Vector2 WanderAround(Bounds bounds)
	{
		return new Vector2(Random.Range(bounds.min.x + 0.3f, bounds.max.x - 0.3f), Random.Range(bounds.min.y + 0.75f, bounds.max.y - 0.75f));
	}

	//Check whether the werewolf is entirely inside the train car
	public bool IsInside(Collider2D coll){
		Vector2 pointA = new Vector2(myCollider.bounds.max.x, myCollider.bounds.max.y);
		Vector2 pointB = new Vector2(myCollider.bounds.max.x, myCollider.bounds.min.y);
		Vector2 pointC = new Vector2(myCollider.bounds.min.x, myCollider.bounds.max.y);
		Vector2 pointD = new Vector2(myCollider.bounds.min.x, myCollider.bounds.min.y);

		if (coll.bounds.Contains(pointA) && coll.bounds.Contains(pointB) && coll.bounds.Contains(pointC) && coll.bounds.Contains(pointD))
		{
			return true;
		}

		return false;
	}

	//Make sure the werewolf's new position isn't outside the train car's bounds
	public Vector2 PosInTrainCar(Vector2 pos, Bounds carBounds){
		if (vertCollider.enabled)
		{
			if (pos.x < carBounds.min.x + 0.3f)
			{
				pos.x = carBounds.min.x + 0.3f;
			}
			else if (pos.x > carBounds.max.x - 0.3f)
			{
				pos.x = carBounds.max.x - 0.3f;
			}

			if (pos.y < carBounds.min.y + 0.75f)
			{
				pos.y = carBounds.min.y + 0.75f;
			}
			else if (pos.y > carBounds.max.y - 0.75f)
			{
				pos.y = carBounds.max.y - 0.75f;
			}
		}
		else {
			if (pos.x < carBounds.min.x + 0.8f)
			{
				pos.x = carBounds.min.x + 0.8f;
			}
			else if (pos.x > carBounds.max.x - 0.8f)
			{
				pos.x = carBounds.max.x - 0.8f;
			}

			if (pos.y < carBounds.min.y + 0.4f)
			{
				pos.y = carBounds.min.y + 0.4f;
			}
			else if (pos.y > carBounds.max.y - 0.4f)
			{
				pos.y = carBounds.max.y - 0.4f;
			}
		}

		return pos;
	}

	//Set animation according to sprite movement
	private void SetAnimation(Vector2 startPos, Vector2 newPos, bool hasTarget)
	{
		//Change in X and Y Positions
		float deltaX = newPos.x - startPos.x;
		float deltaY = newPos.y - startPos.y;

		//Check if werewolf is moving
		if (!hasTarget && (Mathf.Abs(deltaX) > .0001f || Mathf.Abs(deltaY) > .0001f))
		{
			myAnimator.SetBool("moving", true);

			if (Mathf.Abs(deltaX) > Mathf.Abs(deltaY))
			{
				vertCollider.enabled = false;
				horizCollider.enabled = true;

				myCollider = horizCollider;

				myAnimator.SetBool("forward", false);
				myAnimator.SetBool("backward", false);

				if (deltaX > 0)
				{
					myAnimator.SetBool("right", true);
					myAnimator.SetBool("left", false);
				}
				else
				{
					myAnimator.SetBool("left", true);
					myAnimator.SetBool("right", false);
				}
			}
			else
			{
				horizCollider.enabled = false;
				vertCollider.enabled = true;

				myCollider = vertCollider;

				myAnimator.SetBool("left", false);
				myAnimator.SetBool("right", false);

				if (deltaY > 0)
				{
					myAnimator.SetBool("backward", true);
					myAnimator.SetBool("forward", false);
				}
				else
				{
					myAnimator.SetBool("forward", true);
					myAnimator.SetBool("backward", false);
				}
			}
		}
		else if (!hasTarget)
		{
			myAnimator.SetBool("moving", false);
		}
		else
		{
			myAnimator.SetBool("moving", false);

			if (Mathf.Abs(deltaX) > Mathf.Abs(deltaY))
			{
				vertCollider.enabled = false;
				horizCollider.enabled = true;

				myCollider = horizCollider;

				myAnimator.SetBool("forward", false);
				myAnimator.SetBool("backward", false);

				if (deltaX > 0)
				{
					myAnimator.SetBool("right", true);
					myAnimator.SetBool("left", false);
				}
				else
				{
					myAnimator.SetBool("left", true);
					myAnimator.SetBool("right", false);
				}
			}
			else
			{
				vertCollider.enabled = true;
				horizCollider.enabled = false;

				myCollider = vertCollider;

				myAnimator.SetBool("left", false);
				myAnimator.SetBool("right", false);

				if (deltaY > 0)
				{
					myAnimator.SetBool("backward", true);
					myAnimator.SetBool("forward", false);
				}
				else
				{
					myAnimator.SetBool("forward", true);
					myAnimator.SetBool("backward", false);
				}
			}
		}
	}

	//Flash red when damaged
	IEnumerator IndicateDamage()
	{
		myRenderer.color = new Color(1.0f, 0.0f, 0.0f, 1.0f);
		yield return new WaitForSeconds(.1f);
		myRenderer.color = new Color(1.0f, 1.0f, 1.0f, 1.0f);
	}


	//Flash green when healed
	IEnumerator IndicateHeal()
	{
		myRenderer.color = new Color(0.0f, 1.0f, 0.0f, 1.0f);
		yield return new WaitForSeconds(.1f);
		myRenderer.color = new Color(1.0f, 1.0f, 1.0f, 1.0f);
	}
}