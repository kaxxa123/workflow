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


contract('Testing Workflow Abort', function (accounts) {

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

        await wf.doInit(1, [makeDocID(0, 1000), makeDocID(1, 101)], [0x111,0x112], {from: accounts[1]})
        await wf.doApprove(2, {from: accounts[1]})
        await wf.doApprove(3, {from: accounts[2]})
        await wf.doReview([makeDocID(1, 101)], [makeDocID(1, 2000), makeDocID(1, 2001)], [0x111,0x112], {from: accounts[1]});
        await wf.doReview([], [makeDocID(2, 0x333)], [0x333], {from: accounts[1]});
        await wf.doReview([], [makeDocID(3, 0x444), makeDocID(3, 0x555)], [0x444,0x555], {from: accounts[1]});
        await wf.doReview([makeDocID(2, 0x333), makeDocID(1, 2001)], [], [], {from: accounts[1]});

    });

    it('Should fail to Abort WF', async () => {
        let wf = await Workflow.at(wfAddr);

        //Sender has no Right to perform this action
        await HlpFail.testFail("wf.doAbort", "Unauthorized state crossing", async () => { 
            await wf.doAbort(4) 
        });

        //S3 -> S2 is Approve not a Abort edge
        await HlpFail.testFail("wf.doAbort", "Unauthorized state crossing", async () => { 
            await wf.doAbort(2, {from: accounts[1]}) 
        });

        //S3 -> S4 is Sign-Off not a Abort edge
        await HlpFail.testFail("wf.doAbort", "Unauthorized state crossing", async () => { 
            await wf.doAbort(4, {from: accounts[1]}) 
        });

        //S15 does not exist
        await HlpFail.testFail("wf.doAbort", "Non-existing state", async () => { 
            await wf.doAbort(15, {from: accounts[1]}) 
        });
    });

    it('Should Cross State to SignOff Ok', async () => {
        let wf = await Workflow.at(wfAddr);
        await wf.doAbort(5, {from: accounts[1]});

        state = await wf.state()
        mode = await wf.mode()
        totDocs = await wf.totalDocTypes()
        totHist = await wf.totalHistory()
        assert(state == 5, "Should be State 4");
        assert(mode == WFMode.ABORTED, "Should be ABORTED State");
        assert(totDocs == 4, "Total docs should be 4");
        assert(totHist == 8, "Total history should be 8");

        await wfhlp.showHistory(wf);
        await wfhlp.showLatest(wf);
    });
});
