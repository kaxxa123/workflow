# Creating a Workflow Instance – Workflow Contract

The ``Workflow`` smart contract keeps track of a specific WF instance. This is initialized with the WF definition created using ``WorkflowBuilder`` and the Document Set: <BR />
``constructor (IStateEngine eng, uint256[] memory docs)``

<BR />

## The Document Set

The document set defines a set of properties for the documents that will be traversing the WF. In all each document set entry has 4 properties:

1.	__Document Type__ (``DocType``) – A number identifying the document. The smart contract does not get into the true meaning of this value. For example, the business logic could define the DocType as follows: <BR />
    0 = Quotation <BR />
    1 = Invoice

    Note, the ``DocType`` value is not directly specified at the constructor ``docs`` array. Instead, this is derived from the ``docs`` indexes. The first array entry has ``DocType``=0, next is 1 and so on.


1. __Low Limit__ – Defines the minimum number of document instances the WF must include for this ``DocType``.

1.	__Hight Limit__ – Defines the maximum number of document instances the WF must include for this ``DocType``.

1. __Flags__ – A 32-bit flag field defining <BR />
    ``FLAG_REQUIRED`` – WF must always include an instance of this ``DocType`` <BR />
    ``FLAG_PUBLIC`` – Document instances of this ``DocType`` are for public consumption.

The document set properties are fed to the constructor as an array of uint256 entries. Each is encoded as follows: <BR />
``free | flags | hiLimit | loLimit``

Within Workflow, the document set is stored as follows: <BR />
``mapping(uint32 => DocProps) private docSet;`` <BR />
``uint32 public totalDocTypes;``

Where the mapping is made up of: <BR />
``DocType => DocProps``

Where ``DocProps`` is a structure containing the ``loLimit``, ``hiLimit``, ``flags`` and the current document instance count.


<BR />

## Traversing the Workflow

When a ``Workflow`` contract is instantiated, this is set to be on State 0. Thereafter workflow participants can call any of the ``doXXX`` functions to move the documents across states. Here we have exactly 1 function for each possible action/right:


| Action    |  Function |
|-----------|-----------------------------------------------------------------------|
| INIT	    | ``doInit(uint32 nextState, uint256[] ids, uint256[]content)``         | 
| APPROVE	| ``doApprove(uint32 nextState)`` | 
| REVIEW	| ``doReview(uint256[] idsRmv, uint256[] idsAdd, uint256[] contentAdd)`` | 
| SIGNOFF	| ``doSignoff(uint32 nextState)`` | 
| ABORT	    | ``doAbort(uint32 nextState)`` | 


``doInit`` and ``doReview`` are the only actions where a WF participant may submit document updates.

``doApprove``, ``doSignoff`` and ``doAbort`` only allow the WF to transition to the next state without updating the documents.

``doInit`` requires the initial document instances that will start-off this workflow. This must include all documents marked as required.


``doReview`` allows for removing, adding, and updating documents.


<BR />

## Document Updates

``doInit`` and ``doReview`` take as input a set of ``uint256`` arrays: <BR />
``doInit(uint32 nextState, uint256[] ids, uint256[]content)`` <BR />
``doReview(uint256[] idsRmv, uint256[] idsAdd, uint256[] contentAdd)`` <BR />

In these functions ``content`` and ``contentAdd`` are arrays of hashes providing unique signatures for the added/updated documents. Whereas ``ids``, ``idsRmv`` and ``idsAdd`` are document ids.

A document ID is constructed as follows: <BR />
``uid | docType``

Here the lower 32-bits identifies the ``DocType``, matching one of the document-set entries. The remaining bits are set by the caller to something unique. For a specific document, this id must stay fixed throughout the WF. 

From the id, ``Workflow`` identifies when a new document is being added and when an existing document is being updated. 

In ``doInit``, ``ids`` and ``content`` arrays must have the same length. The 2 arrays are referring to the same document instance list. The first is specifying the document id and the second is specifying its hash.

In ``doReview``, ``idsRmv`` identifies a set of document instances that are to be removed. On the other hand, ``idsAdd`` and ``contentAdd`` identify documents to be added/updated. Again, the ``xxxAdd`` arrays must have the same length.

<BR />

## Document Information

The document instance information is stored in two state variables: <BR />
``mapping(uint256 => uint256) public latest;`` <BR />
``HistoryInfo[] private history;``

``latest`` is a mapping between ids and hashes. It stores the current hashes of active documents. Entries are added/removed as documents are added/deleted. When a document is updated, its hash is updated here too.

``history`` stores an array of WF actions. For every successful ``doXXX`` function call, an entry is added to this array. A ``history`` entry includes the parameters passed to the ``doXXX`` functions plus the sender address and the action performed. From ``history`` one could replay every WF action and state transition performed. One may want to consider eliminating ``history`` replacing it with events so as to save on gas.

The ``Workflow`` does not provide a direct method for clients to enumerate the current list of documents. It is up to the client applications for them to keep track of the documents. At worse one can discover the current documents from ``history``.

A client application would read ``history`` and add/delete updates for each ``INIT``/``REVIEW`` action until reaching the end.

``totalHistory()`` returns the total history item elements. This value can also be used to detect changes within the workflow just like a usn.

