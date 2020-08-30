const WorkflowBuilder = artifacts.require("WorkflowBuilder");
const WorkflowManager = artifacts.require("WorkflowManager");
const Workflow = artifacts.require("Workflow");
const truffleAssert = require('truffle-assertions');
const HlpFail = require('./helpers/testFailure');
const wfhlp = require('./helpers/wfhelp');

const WFFlags = wfhlp.WFFlags;
const WFMode = wfhlp.WFMode;
const makeDocSet = wfhlp.makeDocSet;

contract('Testing WorkflowManager addWF', function (accounts) {

    beforeEach( () => {
        console.log();
        console.log("Running new test...");
    });

    var wfAddrs = new Array();

    let docSet = [makeDocSet(1,1,WFFlags.REQUIRED),
        makeDocSet(1,2,WFFlags.REQUIRED),
        makeDocSet(1,2,0),
        makeDocSet(2,2,0)]; 

    it('should fail to create WF (invalid param)', async () => {

        let engine = await WorkflowBuilder.deployed();
        let mgr = await WorkflowManager.deployed();

        let WF_ADMIN_ROLE = await mgr.WF_ADMIN_ROLE();
        await mgr.grantRole(WF_ADMIN_ROLE, accounts[0]);

        await HlpFail.testFail("mgr.addWF", "Invalid State Engine address", async () => { 
            await mgr.addWF("0x0000000000000000000000000000000000000000", docSet);
        });

        await HlpFail.testFail("mgr.addWF", "Empty Doc Set", async () => { 
            await mgr.addWF(engine.address, []);
        });

        await HlpFail.testFail("mgr.addWF", "Unauthorized", async () => { 
            await mgr.addWF(engine.address, docSet, {from: accounts[1]});
        });
    });

    it('should create 17 WFs', async () => {
        let engine = await WorkflowBuilder.deployed();
        let mgr = await WorkflowManager.deployed();

        for (var count = 0; count < 17; ++count) {

            let recpt = await mgr.addWF(engine.address, docSet);
            truffleAssert.eventEmitted(recpt, 'EventWFAdded', (ev) => {
                wfAddrs[count] = ev.addr;
                return (ev.id == (count+1)); 
            });
            truffleAssert.eventNotEmitted(recpt, 'EventWFDeleted');

            console.log("WF Creatred: ", wfAddrs[count]);
        }

        let first = await mgr.firstWF();
        let last = await mgr.lastWF();
        let totalWFs = await mgr.totalWFs();
        let next = await mgr.nextWF();
        let closed = await mgr.totalClosedWFs()
        assert(first == 1, "First WF should have index 1");
        assert(last == 17, "Last WF should have index 17");
        assert(totalWFs == 17, "Number of created WFs should be 17");
        assert(next == 18, "Next WF should have idx 18");
        assert(closed == 0, "Closed WFs should be 0");
    });

    it('should read WFs 1 by 1 and confirm expected settings', async () => {
        let engine = await WorkflowBuilder.deployed();
        let mgr = await WorkflowManager.deployed();
        let next = await mgr.firstWF();;
        let count = 0;
        do 
        {
            let ret = await mgr.readWF(next, 1);
            assert(ret.addrList[0] == wfAddrs[count], "Unexpected Address");

            let wf = await Workflow.at(ret.addrList[0]);

            let state = await wf.state();
            let mode = await wf.mode();
            let id = await wf.wfID();
            let engAddr = await wf.engine();
            let totDocTypes = await wf.totalDocTypes();
            let totHist = await wf.totalHistory();

            assert(state == 0, "WF must be in S0");
            assert(mode == WFMode.UNINIT, "WF should be UNINIT");
            assert(id == (count+1), `WF id should be ${count+1}`);
            assert(totDocTypes == docSet.length, `Total Doc types should be ${docSet.length}`);
            assert(totHist == 0, "History total should be 0");
            assert(engAddr == engine.address, "Engine address mistmatched");

            count += 1;
            next   = ret.next;
            console.log("Address/Next: " + ret.addrList[0] + " / "+ ret.next);

        } while (next != 0)

        assert(count == 17, "Should have read 17 entries")
    });
});
