const WorkflowBuilder = artifacts.require("WorkflowBuilder");
const WorkflowManager = artifacts.require("WorkflowManager");
const Workflow = artifacts.require("Workflow");
const truffleAssert = require('truffle-assertions');
const wfhlp = require('./helpers/wfhelp');

const WFRights = require('./helpers/wfhelp').WFRights;
const WFFlags = wfhlp.WFFlags;
const makeDocSet = wfhlp.makeDocSet;
const makeDocID = require('./helpers/wfhelp').makeDocID;

contract('Testing WorkflowManager readWF', function (accounts) {

    beforeEach( () => {
        console.log();
        console.log("Running new test...");
    });

    var wfAddrs = new Array();

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

    const signOffWF = async (mgr, id) => {

        let retAddr = await mgr.readWF(id, 1);
        let wf = await Workflow.at(retAddr.addrList[0]);
        await wf.doInit(0, 1, [makeDocID(0, 1000), makeDocID(1, 101)], [0x111,0x112], {from: accounts[1]})
        await wf.doApprove(1, 2, {from: accounts[1]})
        await wf.doApprove(2, 3, {from: accounts[2]})
        await wf.doSignoff(3, 4, {from: accounts[1]});
    }

    it('should create 17 WFs', async () => {
        let engine = await WorkflowBuilder.deployed();
        let mgr = await WorkflowManager.deployed();

        let WF_ADMIN_ROLE = await mgr.WF_ADMIN_ROLE();
        await mgr.grantRole(WF_ADMIN_ROLE, accounts[0]);

        await setupStateEngine(engine);

        let participants = await wfhlp.getParticipants(engine);
        console.log("Participants:");
        console.log(participants);
        console.log();

        //Create 17 WFs
        for (var count = 0; count < 17; ++count) {

            let recpt = await mgr.addWF(engine.address, docSet);
            truffleAssert.eventEmitted(recpt, 'EventWFAdded', (ev) => {
                    wfAddrs[count] = ev.addr;
                    return (ev.id == (count+1)); 
                });
        }
    });

    it('should read WFs using page sizes from 1 to 17', async () => {
        let mgr = await WorkflowManager.deployed();

        for (pagesz = 1; pagesz < 18; ++pagesz) {

            let stack = new Array();
            let next = await mgr.firstWF();
            let rdCount = 0;

            //Read addresses using given page size
            do {
                rdCount += 1;
                let ret = await mgr.readWF(next, pagesz);

                stack.push(ret);
                next = ret.next;
            } while (next != 0)

            assert(rdCount == Math.ceil(17/pagesz), "Unexpected # of reads");

            //flatten the address list
            var flat = new Array();

            stack.every((value, index) => {
                flat = flat.concat(value.addrList);
                return true;
            });

            //compare the output list with expected list
            assert((wfAddrs.length === flat.length) && 
            wfAddrs.every((value, index) => value === flat[index]),
            "Unexpected mistmatch in address list when page size is: " + pagesz);
        }
    });

    it('should read WFs despite interleaving deletions', async () => {
        let mgr = await WorkflowManager.deployed();

        let pagesz = 3;
        let stack = new Array();
        let next = await mgr.firstWF();
        let bkCount = 0;

        do {
            //insert deletions to cause failures
            if ((bkCount == 0) && (next == 10)) {
                bDel = false;
                await signOffWF(mgr, 10);
                await signOffWF(mgr, 7);

                wfAddrs.splice(10-1,1);
                wfAddrs.splice(7-1,1);
            }

            try {
                console.log("Reading from: " + next);
                let ret = await mgr.readWF(next, pagesz);

                ///TRICK - TRICK - TRICK - TRICK 
                //Here the pushed stack value is different than usual
                //since to be able to back-track we need prev not next
                let prev = next;
                next = ret.next;

                ret.next = prev;
                stack.push(ret);
            }
            catch (err) {
                ++bkCount;

                if (!err.message.includes('Invalid reading position'))
                    assert(false, "Unexpected error: " + err.message);

                //back track to try and get a correct page start
                if (stack.length == 0)
                    next = await mgr.firstWF();
                else {
                    let bkRet = stack.pop();
                    next = bkRet.next;
                }
            }

        } while (next != 0)

        //Expected back track steps
        assert(bkCount == 2, "Unexpected back track count: " + bkCount);

        //flatten the address list
        var flat = new Array();

        stack.every((value, index) => {
            flat = flat.concat(value.addrList);
            return true;
        });

        //compare the output list with expected list
        assert((wfAddrs.length === flat.length) && 
        wfAddrs.every((value, index) => value === flat[index]),
        "Unexpected mistmatch in address list when page size is: " + pagesz);
    });

    it('should read empty WFs array', async () => {
        let mgr = await WorkflowManager.deployed();

        let pagesz = 3;
        let stack = new Array();
        let next = await mgr.firstWF();

        do {
            if ((next == 12)) {
                //delete all WFs
                while (true) {
                    let pos = await mgr.firstWF();
                    if (pos == 0) break;

                    await signOffWF(mgr, pos);
                }
            }

            try {
                console.log("Reading from: " + next);
                let ret = await mgr.readWF(next, pagesz);

                ///TRICK - TRICK - TRICK - TRICK 
                //Here the pushed stack value is different than usual
                //since to be able to back-track we need prev not next
                let prev = next;
                next = ret.next;

                ret.next = prev;
                stack.push(ret);
            }
            catch (err) {
                if (!err.message.includes('Invalid reading position'))
                    assert(false, "Unexpected error: " + err.message);

                //back track to try and get a correct page start
                if (stack.length == 0)
                    next = await mgr.firstWF();
                else {
                    let bkRet = stack.pop();
                    next = bkRet.next;
                }
            }

        } while (next != 0)

        assert(stack.length == 0, "Unexpected we should have an empty array");
    });    
});
