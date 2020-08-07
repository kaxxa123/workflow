
async function testFail(name, errMsg, lambda) 
{
    console.log("Testing Failure of: " + name + "()...");
    try {
        await lambda();
        assert(false, "Should have failed!");
    }
    catch(err) {
        if (!err.message.includes(errMsg))
                assert(false, "Unexpected error: " + err.message);
        else    console.log("Error Confirmed: " + err.message);
    }
}

module.exports = {
    testFail
}
