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

contract('Testing Workflow Approve', function (accounts) {

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
    });

    it('Should fail to Approve WF', async () => {

        let wf = await Workflow.at(wfAddr);

        //Sender has no Right to perform this action
        await HlpFail.testFail("wf.doApprove", "Unauthorized state crossing", async () => { 
            await wf.doApprove(2) 
        });

        //No edge between S1 and S3
        await HlpFail.testFail("wf.doApprove", "Unauthorized state crossing", async () => { 
            await wf.doApprove(3, {from: accounts[1]}) 
        });

        //S15 does not exist
        await HlpFail.testFail("wf.doApprove", "Non-existing state", async () => { 
            await wf.doApprove(15, {from: accounts[1]}) 
        });
    });

    it('Should fail to perform any action other than Approve', async () => {

        let wf = await Workflow.at(wfAddr);

        //From S1 only only an Approve action is configured
        await HlpFail.testFail("wf.doInit", "Unauthorized state crossing", async () => { 
            await wf.doInit(1, [makeDocID(0, 1000), makeDocID(1, 101)], [0x111,0x112], {from: accounts[1]})
        });

        //S15 does not exist
        await HlpFail.testFail("wf.doInit", "Non-existing state", async () => { 
            await wf.doInit(15, [makeDocID(0, 1000), makeDocID(1, 101)], [0x111,0x112], {from: accounts[1]})
        });

        //From S1 only only an Approve action is configured
        await HlpFail.testFail("wf.doReview", "Unauthorized state crossing", async () => { 
            await wf.doReview([1],[1],[1], {from: accounts[1]})
        });

        //From S1 only only an Approve action is configured
        await HlpFail.testFail("wf.doSignoff", "Unauthorized state crossing", async () => { 
            await wf.doSignoff(1, {from: accounts[1]})
        });

        //S15 does not exist
        await HlpFail.testFail("wf.doSignoff", "Non-existing state", async () => { 
            await wf.doSignoff(15, {from: accounts[1]})
        });

        //From S1 only only an Approve action is configured
        await HlpFail.testFail("wf.doAbort", "Unauthorized state crossing", async () => { 
            await wf.doAbort(1, {from: accounts[1]})
        });

        //S15 does not exist
        await HlpFail.testFail("wf.doAbort", "Non-existing state", async () => { 
            await wf.doAbort(15, {from: accounts[1]})
        });
    });

    it('Should Cross State to Apporve Ok', async () => {
        let wf = await Workflow.at(wfAddr);

        //S1 -> S2
        await wf.doApprove(2, {from: accounts[1]})

        let state = await wf.state()
        let mode = await wf.mode()
        let totDocs = await wf.totalDocTypes()
        let totHist = await wf.totalHistory()
        assert(state == 2, "Should be State 2");
        assert(mode == WFMode.RUNNING, "Should be RUNNING State");
        assert(totDocs == 4, "Total docs should be 4");
        assert(totHist == 2, "Total history should be 1");

        //S2 -> S3
        await wf.doApprove(3, {from: accounts[2]})

        state = await wf.state()
        mode = await wf.mode()
        totDocs = await wf.totalDocTypes()
        totHist = await wf.totalHistory()
        assert(state == 3, "Should be State 3");
        assert(mode == WFMode.RUNNING, "Should be RUNNING State");
        assert(totDocs == 4, "Total docs should be 4");
        assert(totHist == 3, "Total history should be 1");

        //S3 -> S1 -> S2
        await wf.doApprove(1, {from: accounts[1]})
        await wf.doApprove(2, {from: accounts[1]})

        state = await wf.state()
        mode = await wf.mode()
        totDocs = await wf.totalDocTypes()
        totHist = await wf.totalHistory()

        assert(state == 2, "Should be State 2");
        assert(mode == WFMode.RUNNING, "Should be RUNNING State");
        assert(totDocs == 4, "Total docs should be 4");
        assert(totHist == 5, "Total history should be 1");
        await wfhlp.showHistory(wf);
        await wfhlp.showLatest(wf);
    });

    it('Should fail to Approve WF', async () => {

        let wf = await Workflow.at(wfAddr);

        //Sender has no Right to perform this action
        await HlpFail.testFail("wf.doApprove", "Unauthorized state crossing", async () => { 
            await wf.doApprove(2) 
        });

        //Edge between S2 to S4 does not exist
        await HlpFail.testFail("wf.doApprove", "Unauthorized state crossing", async () => { 
            await wf.doApprove(4, {from: accounts[2]}) 
        });

        //S15 does not exist
        await HlpFail.testFail("wf.doApprove", "Non-existing state", async () => { 
            await wf.doApprove(15, {from: accounts[2]}) 
        });
    });

    it('Should fail to perform any action other than Approve', async () => {

        let wf = await Workflow.at(wfAddr);

        //From S2 only only an Approve action is configured
        await HlpFail.testFail("wf.doInit", "Unauthorized state crossing", async () => { 
            await wf.doInit(1, [makeDocID(0, 1000), makeDocID(1, 101)], [0x111,0x112], {from: accounts[2]})
        });

        //S15 does not exist
        await HlpFail.testFail("wf.doInit", "Non-existing state", async () => { 
            await wf.doInit(15, [makeDocID(0, 1000), makeDocID(1, 101)], [0x111,0x112], {from: accounts[2]})
        });

        //From S2 only only an Approve action is configured
        await HlpFail.testFail("wf.doReview", "Unauthorized state crossing", async () => { 
            await wf.doReview([1],[1],[1], {from: accounts[2]})
        });

        //From S2 only only an Approve action is configured
        await HlpFail.testFail("wf.doSignoff", "Unauthorized state crossing", async () => { 
            await wf.doSignoff(1, {from: accounts[2]})
        });

        //S15 does not exist
        await HlpFail.testFail("wf.doSignoff", "Non-existing state", async () => { 
            await wf.doSignoff(15, {from: accounts[2]})
        });

        //From S2 only only an Approve action is configured
        await HlpFail.testFail("wf.doAbort", "Unauthorized state crossing", async () => { 
            await wf.doAbort(1, {from: accounts[2]})
        });

        //S15 does not exist
        await HlpFail.testFail("wf.doAbort", "Non-existing state", async () => { 
            await wf.doAbort(15, {from: accounts[2]})
        });
    });

    it('Should Cross State to Apporve Ok2', async () => {
        let engine = await WorkflowBuilder.deployed();
        let wf = await Workflow.at(wfAddr);

        //S2 -> S3 -> S2 -> S3 -> S2
        await wf.doApprove(3, {from: accounts[2]});
        await wf.doApprove(2, {from: accounts[1]});
        await wf.doApprove(3, {from: accounts[2]});
        await wf.doApprove(2, {from: accounts[1]});

        //Remove user right and see it fail...
        await engine.removeRight(2, 0, accounts[2], WFRights.APPROVE);        //S2 -> S3
        await HlpFail.testFail("wf.doApprove", "Unauthorized state crossing", async () => { 
            await wf.doApprove(3, {from: accounts[2]}); 
        });

        //Add different user right and see old user fail new user succeed...
        await engine.addRight(2, 0, accounts[3], WFRights.APPROVE);           //S2 -> S3
        await HlpFail.testFail("wf.doApprove", "Unauthorized state crossing", async () => { 
            await wf.doApprove(3, {from: accounts[2]}); 
        });
        await wf.doApprove(3, {from: accounts[3]});

        state = await wf.state()
        mode = await wf.mode()
        totDocs = await wf.totalDocTypes()
        totHist = await wf.totalHistory()
        assert(state == 3, "Should be State 3");
        assert(mode == WFMode.RUNNING, "Should be RUNNING State");
        assert(totDocs == 4, "Total docs should be 4");
        assert(totHist == 10, "Total history should be 10");

        await wfhlp.showHistory(wf);
        await wfhlp.showLatest(wf);
    });

});
