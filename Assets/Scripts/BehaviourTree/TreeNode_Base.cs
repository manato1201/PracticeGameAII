using System.Collections.Generic;
using Unity.PlasticSCM.Editor.WebApi;
using UnityEngine;

// ビヘイビアツリーの結果の定義
public enum BT_Status
{
	SUCCESS,	// 成功（完了）
	FAILURE,	// 失敗
	RUNNING		// 実行中
};

// ビヘイビアツリーのノードの基底クラス
/* すべてのノードはこのクラスを継承して
	- コンストラクタ
	- OnEnter() (必要な場合のみ)
	- OnUpdate() 
	を実装する
	
	ルートノードを実行する時は ExecuteAsRoot() を呼び、
	親ノードから子ノードを実行する時は ExecuteChild() を呼び出す
*/
public class TreeNode_Base
{
	// 子ノードを格納するリスト
	protected List<TreeNode_Base> childrenNodes = new List<TreeNode_Base>();
	// 対象となるGameObject
	protected Bat bat;
	// ノードを（あらためて）実行するたびにOnEnter()を呼び出すようにする
	private TreeNode_Base previousNode = null;
	private bool firstRootCall = false;

	// ノードのコンストラクタ	
	/* OnEnter() を実装するための実行チェックが必要なので、ノード毎に対象となるGameObject は個別に設定することにする
	それが必要なければ、Bat b を Update() の引数にすることで、ひとつのインスタンスで複数の GameObject を扱うこともできる（メモリメリット）
	*/
	public TreeNode_Base(Bat b)
	{
		bat = b;
	}
	
	// ルートノードとして実行する時
	public BT_Status ExecuteAsRoot()
	{
		if(!firstRootCall){
			firstRootCall = true;
			OnEnter();
		}
		return OnUpdate();
	}
	
	// 子ノードを呼び出すときは、ExecuteChild()を使う
	public BT_Status ExecuteChild(int idx)
	{
		if(idx < 0 || idx >= childrenNodes.Count){
			previousNode = null;
			return BT_Status.FAILURE;
		}
		TreeNode_Base currentNode = childrenNodes[idx];
		if(previousNode != currentNode){
			previousNode = currentNode;
			currentNode.OnEnter();
		}
		return currentNode.OnUpdate();
	}
	
	// ノードの処理を始めるときに一度だけ呼び出される
	// オーバーライドする時は派生クラス側からも呼び出すこと
	protected virtual void OnEnter()
	{
		previousNode = null;
	}
	
	// このノードで実行することを設定する
	protected virtual BT_Status OnUpdate()
	{
		// 必ず実装する
		Debug.Log("TreeNode_Base - OnUpdate() - Override required");
		return BT_Status.RUNNING;
	}
}

