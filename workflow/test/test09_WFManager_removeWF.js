const WorkflowBuilder = artifacts.require("WorkflowBuilder");
const WorkflowManager = artifacts.require("WorkflowManager");
const Workflow = artifacts.require("Workflow");
const BigNumber = require("bignumber.js");
const truffleAssert = require('truffle-assertions');
const truffleEvent  = require('truffle-events');
const HlpFail = require('./helpers/testFailure');
const wfhlp = require('./helpers/wfhelp');

const WFRights = require('./helpers/wfhelp').WFRights;
const WFFlags = wfhlp.WFFlags;
const WFMode = require('./helpers/wfhelp').WFMode;
const makeDocSet = wfhlp.makeDocSet;
const makeDocID = require('./helpers/wfhelp').makeDocID;

contract('Testing WorkflowManager removeWF', function (accounts) {

    beforeEach( () => {
        console.log();
        console.log("Running new test...");
    });

    let docSet = [makeDocSet(1,1,WFFlags.REQUIRED),
        makeDocSet(1,2,WFFlags.REQUIRED),
        makeDocSet(1,2,0),
        makeDocSet(2,2,0)]; 


    const setupStateEngine = async (engine) => {
        let WF_SCHEMA_ADMIN_ROLE = await engine.WF_SCHEMA_ADMIN_ROLE();
        await engine.grantRole(WF_SCHEMA_ADMIN_ROLE, accounts[0]);

        await engine.addState([1]);
        await engine.addState([2]);
        await engine.addState([3]);
        await engine.addState([1,2,3,4,5]);
        await engine.addState([]);
        await engine.addState([]);
        await engine.doFinalize();
        await engine.addRight(0, 0, accounts[1], WFRights.INIT);       //S0 -> S1
        await engine.addRight(1, 0, accounts[1], WFRights.APPROVE);    //S1 -> S2
        await engine.addRight(2, 0, accounts[2], WFRights.APPROVE);    //S2 -> S3
        await engine.addRight(3, 0, accounts[1], WFRights.APPROVE);    //S3 -> S1
        await engine.addRight(3, 1, accounts[1], WFRights.APPROVE);    //S3 -> S2
        await engine.addRight(3, 2, accounts[1], WFRights.REVIEW);     //S3 -> S3
        await engine.addRight(3, 3, accounts[1], WFRights.SIGNOFF);    //S3 -> S4
        await engine.addRight(3, 4, accounts[1], WFRights.ABORT);      //S3 -> S5
    }

    it('Should create and delete WF through Workflow.doSignOff()', async () => {

        let engine = await WorkflowBuilder.deployed();
        let mgr = await WorkflowManager.deployed();
        var wfAddr;
        var wfId;

        let WF_ADMIN_ROLE = await mgr.WF_ADMIN_ROLE();
        await mgr.grantRole(WF_ADMIN_ROLE, accounts[0]);

        let recpt = await mgr.addWF(engine.address, docSet);
        truffleAssert.eventEmitted(recpt, 'EventWFAdded', (ev) => {
                wfId = ev.id;
                wfAddr = ev.addr.toString();
                return (ev.id == 1); 
            });

        let totalWFs = await mgr.totalWFs();
        let closed = await mgr.totalClosedWFs()
        assert(totalWFs == 1, "Number of created WFs should be 1");
        assert(closed == 0, "Number of closed WFs should be 0");

        await setupStateEngine(engine);

        let wf = await Workflow.at(wfAddr);
        await wf.doInit(0, 1, [makeDocID(0, 1000), makeDocID(1, 101)], [0x111,0x112], {from: accounts[1]})
        await wf.doApprove(1, 2, {from: accounts[1]})
        await wf.doApprove(2, 3, {from: accounts[2]})

        recpt = await wf.doSignoff(3, 4, {from: accounts[1]});

        let recpt2 = recpt;
        recpt2.receipt.logs = recpt2.receipt.rawLogs;
        recpt2 = truffleEvent.formTxObject('WorkflowManager', 0, recpt2);
        truffleAssert.eventEmitted(recpt2, 'EventWFDeleted', (ev) => { 
                return (ev.id == wfId) && (ev.addr.toLowerCase() == wfAddr.toLowerCase()); 
            });

        let first = await mgr.firstWF();
        let last = await mgr.lastWF();
        let next = await mgr.nextWF();
        totalWFs = await mgr.totalWFs();
        closed = await mgr.totalClosedWFs()
        assert(first == 0, "First WF should have index 0");
        assert(last == 0, "Last WF should have index 0");
        assert(next == 2, "Next WF should have idx 2");
        assert(totalWFs == 0, "Number of created WFs should be 0");
        assert(closed == 1, "Number of closed WFs should be 1");

        let delWF = await mgr.closedWFs(0);
        assert(delWF.toLowerCase() == wfAddr.toLowerCase(), "Closed WF address mis-match");
    });

    it('Should setup 5 test WFs', async () => {
        let engine = await WorkflowBuilder.deployed();
        let mgr = await WorkflowManager.deployed();
        
        //Create 5 WFs
        for (var count = 0; count < 5; ++count) {

            let recpt = await mgr.addWF(engine.address, docSet);
            truffleAssert.eventEmitted(recpt, 'EventWFAdded', (ev) => { 
                    return (ev.id == (count+2)); 
                });
        }
    });

    it('should fail to delete WFs', async () => {
        let mgr = await WorkflowManager.deployed();
        
        //WF 0 can never be deleted
        await HlpFail.testFail("removeWF", "WFManager: Uninitialized WF cannot be deleted", async () => { 
            await mgr.removeWF(0); 
        });

        //WF 1 was already deleted
        await HlpFail.testFail("removeWF", "WFManager: Uninitialized WF cannot be deleted", async () => { 
            await mgr.removeWF(1); 
        });

        //WF 2 cannot be deleted since it's WF is still not concluded
        await HlpFail.testFail("removeWF", "WFManager: WF still not concluded", async () => { 
            await mgr.removeWF(2); 
        });
    });

    it('should delete first WF and update list head', async () => {
        let mgr = await WorkflowManager.deployed();
        let first = BigNumber(await mgr.firstWF());
        let len = BigNumber(await mgr.totalWFs());
        let closed = await mgr.totalClosedWFs();

        let retAddr = await mgr.readWF(first, 1);
        let wf = await Workflow.at(retAddr.addrList[0]);
        await wf.doInit(0, 1, [makeDocID(0, 1000), makeDocID(1, 101)], [0x111,0x112], {from: accounts[1]})
        await wf.doApprove(1, 2, {from: accounts[1]})
        await wf.doApprove(2, 3, {from: accounts[2]})
        recpt = await wf.doSignoff(3, 4, {from: accounts[1]});

        let recpt2 = recpt;
        recpt2.receipt.logs = recpt2.receipt.rawLogs;
        recpt2 = truffleEvent.formTxObject('WorkflowManager', 0, recpt2);
        truffleAssert.eventEmitted(recpt2, 'EventWFDeleted', (ev) => { 
                return (first.minus(ev.id) == 0); 
            });
        
        let newFirst = await mgr.firstWF();
        let newLen = await mgr.totalWFs();
        assert(first.minus(newFirst).toNumber() != 0, "WF list head should have changed.");
        assert(len.minus(newLen).minus(1) == 0, "WF list length should be 1 less");

        let delWF = await mgr.closedWFs(closed);
        assert(delWF.toLowerCase() == retAddr.addrList[0].toLowerCase(), "Closed WF address mis-match");
    });

    it('should delete last WF and update list tail', async () => {

        let mgr = await WorkflowManager.deployed();
        let last = BigNumber(await mgr.lastWF());
        let len = BigNumber(await mgr.totalWFs());
        let closed = await mgr.totalClosedWFs();

        let retAddr = await mgr.readWF(last, 1);
        let wf = await Workflow.at(retAddr.addrList[0]);
        await wf.doInit(0, 1, [makeDocID(0, 1000), makeDocID(1, 101)], [0x111,0x112], {from: accounts[1]})
        await wf.doApprove(1, 2, {from: accounts[1]})
        await wf.doApprove(2, 3, {from: accounts[2]})
        recpt = await wf.doSignoff(3, 4, {from: accounts[1]});

        let recpt2 = recpt;
        recpt2.receipt.logs = recpt2.receipt.rawLogs;
        recpt2 = truffleEvent.formTxObject('WorkflowManager', 0, recpt2);
        truffleAssert.eventEmitted(recpt2, 'EventWFDeleted', (ev) => { 
                return (last.minus(ev.id) == 0); 
            });

        let newLast = await mgr.firstWF();
        let newLen = await mgr.totalWFs();
        assert(last.minus(newLast).toNumber() != 0, "WF list tail should have changed.");
        assert(len.minus(newLen).minus(1) == 0, "WF list length should be 1 less");

        console.log(`Total Closed WFs ${closed}`);
        let delWF = await mgr.closedWFs(closed);
        assert(delWF.toLowerCase() == retAddr.addrList[0].toLowerCase(), "Closed WF address mis-match");
    });    
});