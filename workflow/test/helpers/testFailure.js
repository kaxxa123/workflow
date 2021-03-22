
async function testFail(name, errMsg, lambda) 
{
    console.log("Testing Failure of: " + name + "()...");
    let hasFailed = true;
    try {
        await lambda();
        hasFailed = false;
    }
    catch(err) {
        if (!err.message.includes(errMsg))
                assert(false, "Unexpected error: " + err.message);
        else    console.log("Error Confirmed: " + err.message);
    }

    assert(hasFailed, "Should have failed!");
}

module.exports = {
    testFail
}
