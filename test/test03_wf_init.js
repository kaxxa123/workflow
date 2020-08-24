const WorkflowBuilder = artifacts.require("WorkflowBuilder");
const WorkflowManager = artifacts.require("WorkflowManager");
const Workflow = artifacts.require("Workflow");
const truffleAssert = require('truffle-assertions');
const HlpFail = require('./helpers/testFailure');
const wfhlp = require('./helpers/wfhelp');

const WFRights = wfhlp.WFRights;
const WFFlags = wfhlp.WFFlags;
const WFMode = wfhlp.WFMode;
const makeDocSet = wfhlp.makeDocSet;
const makeDocID = wfhlp.makeDocID;

contract('Testing Workflow Init', function (accounts) {

    var wfAddr;
    var wfID;

    it('Should Create Workflow OK', async () => {

        let engine = await WorkflowBuilder.deployed();
        let mgr = await WorkflowManager.deployed();
        let recpt = await mgr.addWF(engine.address, 
                                [makeDocSet(1,1,WFFlags.REQUIRED),
                                makeDocSet(1,2,WFFlags.REQUIRED),
                                makeDocSet(1,2,0),
                                makeDocSet(2,2,0)]);
                                
        truffleAssert.eventEmitted(recpt, 'EventWFAdded', (ev) => {
            wfID = ev.id; 
            wfAddr = ev.addr; 
            return true; 
        });
        let wf = await Workflow.at(wfAddr);

        let engineAddr = await wf.engine()
        assert(engineAddr == engine.address, "Mismatched engine address")

        await engine.addState([1]);
        await engine.addState([2]);
        await engine.addState([3]);
        await engine.addState([1,2,3,4,5]);
        await engine.addState([]);
        await engine.addState([]);
        await engine.doFinalize();
        await engine.addRight(0, 0, accounts[1], WFRights.INIT);       //S0 -> S1
        await engine.addRight(1, 0, accounts[1], WFRights.APPROVE);    //S1 -> S2
        await engine.addRight(1, 0, accounts[2], WFRights.APPROVE);    //S1 -> S2
        await engine.addRight(2, 0, accounts[2], WFRights.APPROVE);    //S2 -> S3
        await engine.addRight(3, 0, accounts[1], WFRights.APPROVE);    //S3 -> S1
        await engine.addRight(3, 1, accounts[1], WFRights.APPROVE);    //S3 -> S2
        await engine.addRight(3, 2, accounts[1], WFRights.REVIEW);     //S3 -> S3
        await engine.addRight(3, 3, accounts[1], WFRights.SIGNOFF);    //S3 -> S4
        await engine.addRight(3, 4, accounts[1], WFRights.ABORT);      //S3 -> S5
    });

    it('Should fail to init WF', async () => {

        let wf = await Workflow.at(wfAddr);
        let state = await wf.state()
        let mode = await wf.mode()
        let totDocs = await wf.totalDocTypes()
        let totHist = await wf.totalHistory()

        assert(state == 0, "Should be State 0");
        assert(mode == WFMode.UNINIT, "Should be UNINT State");
        assert(totDocs == 4, "Total docs should be 4");
        assert(totHist == 0, "Total history should be 0");

        await wfhlp.showDocSet(wf)

        //Sender has no Right to perform this action
        await HlpFail.testFail("wf.doInit", "Unauthorized state crossing", async () => { 
            await wf.doInit(1, [makeDocID(doctype=0,id=0), makeDocID(1, 1)], [0x111,0x112]) 
        });

        //Invalid parameters - ids array longer the content array
        await HlpFail.testFail("wf.doInit", "ids/content array length mismatch", async () => { 
            await wf.doInit(1, [makeDocID(0, 230), makeDocID(1, 124), makeDocID(2, 123)], [0x111,0x112], {from: accounts[1]}) 
        });

        //DocType=5 is invalid
        await HlpFail.testFail("wf.doInit", "Invalid doc type", async () => { 
            await wf.doInit(1, [makeDocID(1, 111), makeDocID(5, 151)], [0x111,0x112], {from: accounts[1]}) 
        });

        //Doc hash cannot be zero
        await HlpFail.testFail("wf.doInit", "Invalid doc hash", async () => { 
            await wf.doInit(1, [makeDocID(0, 100), makeDocID(1, 221)], [0x111,0], {from: accounts[1]}) 
        });

        //Cannot have 3 documents of type 2 (2 is upper limit)
        await HlpFail.testFail("wf.doInit", "Doc type count exceeded limit", async () => { 
            await wf.doInit(1, [makeDocID(2, 111), makeDocID(2, 222), makeDocID(2, 333)], [0x111,0x112,0x113], {from: accounts[1]}) 
        });

        //Cannot add 2 docs with same id
        await HlpFail.testFail("wf.doInit", "Initializing same ID x times", async () => { 
            await wf.doInit(1, [makeDocID(0, 100), makeDocID(1, 101), makeDocID(1, 101)], [0x111,0x112,0x113], {from: accounts[1]}) 
        });

        //DocType 1 is marked as required but it's not included in doInit
        await HlpFail.testFail("wf.doInit", "Required files missing", async () => { 
            await wf.doInit(1, [makeDocID(0, 100), makeDocID(2, 101)], [0x111,0x112], {from: accounts[1]})
        });

        //DocType 3 is not required. However it has a minimum set to 2. However doInit is only supplied with 1 such docType
        //When a DocType is not required we can either have Zero or at least match the minimum limit.
        await HlpFail.testFail("wf.doInit", "Required files missing", async () => { 
            await wf.doInit(1, [makeDocID(0, 1000), makeDocID(1, 101), makeDocID(3, 101)], [0x111,0x112,0x113], {from: accounts[1]})
        });
    });

    it('Should fail to perform any action other than Init', async () => {
        let wf = await Workflow.at(wfAddr);

        //From S0 only a doInit is allowed
        await HlpFail.testFail("wf.doApprove", "Unauthorized state crossing", async () => { 
            await wf.doApprove(1, {from: accounts[1]})
        });

        //From S15 does not exist
        await HlpFail.testFail("wf.doApprove", "Non-existing state", async () => { 
            await wf.doApprove(15, {from: accounts[1]})
        });

        //From S0 only a doInit is allowed
        await HlpFail.testFail("wf.doReview", "Unauthorized state crossing", async () => { 
            await wf.doReview([1],[1],[1], {from: accounts[1]})
        });

        //From S0 only a doInit is allowed
        await HlpFail.testFail("wf.doSignoff", "Unauthorized state crossing", async () => { 
            await wf.doSignoff(1, {from: accounts[1]})
        });

        //From S15 does not exist
        await HlpFail.testFail("wf.doSignoff", "Non-existing state", async () => { 
            await wf.doSignoff(15, {from: accounts[1]})
        });

        //From S0 only a doInit is allowed
        await HlpFail.testFail("wf.doAbort", "Unauthorized state crossing", async () => { 
            await wf.doAbort(1, {from: accounts[1]})
        });

        //From S15 does not exist
        await HlpFail.testFail("wf.doAbort", "Non-existing state", async () => { 
            await wf.doAbort(15, {from: accounts[1]})
        });
    });

    it('Should perform Init Ok', async () => {
        let wf = await Workflow.at(wfAddr);

        await wf.doInit(1, [makeDocID(0, 1000), makeDocID(1, 101)], [0x111,0x112], {from: accounts[1]})

        let state = await wf.state()
        let mode = await wf.mode()
        let totDocs = await wf.totalDocTypes()
        let totHist = await wf.totalHistory()

        assert(state == 1, "Should be State 1");
        assert(mode == WFMode.RUNNING, "Should be RUNNING State");
        assert(totDocs == 4, "Total docs should be 4");
        assert(totHist == 1, "Total history should be 1");

        await wfhlp.showHistory(wf);
        await wfhlp.showLatest(wf);
    });
});
