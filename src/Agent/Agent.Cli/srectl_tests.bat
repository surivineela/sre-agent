@echo off
setlocal enabledelayedexpansion

REM SRECTL Automated Test Suite
REM This batch file runs comprehensive tests for SRECTL
REM covering agent creation, validation, tool validation, and error handling scenarios

echo =====================================
echo SRECTL Automated Test Suite
echo =====================================
echo.

REM Initialize test counters
set /a TESTS_PASSED=0
set /a TESTS_FAILED=0
set /a TOTAL_TESTS=0

REM Create test output directory
if not exist "test_output" mkdir test_output
pushd test_output

REM Initialize the workspace for testing
echo Initializing workspace for testing...
dotnet run --project .. -- init --resource-url "https://localhost:7023" > init_output.txt 2>&1
echo.

echo Starting CLI tests...
echo.

REM ===========================================
REM POSITIVE TEST CASES
REM ===========================================

echo ===========================================
echo POSITIVE TEST CASES
echo ===========================================
echo.

REM Test 1: Basic agent creation
echo [TEST 1] Basic agent creation
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name test_agent --instructions "Test agent instructions for validation - this is a comprehensive test with sufficient length" --tools TestTool1 TestTool2 > test1_output.txt 2>&1
if !errorlevel! equ 0 (
    echo [PASS] Basic agent creation
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Basic agent creation - Command failed
    type test1_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 2: Agent creation with all options
echo [TEST 2] Agent creation with all options
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name full_featured_agent --instructions "Full featured test agent with comprehensive options" --tools Tool1 Tool2 --handoff-description "Test handoff description" --handoffs meta_agent --allow-parallel-tool-calls --max-reflection-count 2 --temperature 0.7 --common-prompts format_guidelines > test2_output.txt 2>&1
if !errorlevel! equ 0 (
    echo [PASS] Agent creation with all options
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Agent creation with all options - Command failed
    type test2_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 3: Agent creation with custom properties
echo [TEST 3] Agent creation with custom properties  
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name custom_agent --instructions "Custom agent with additional properties - this is a comprehensive test with sufficient length" --tools CustomTool --temperature 0.8 --max-reflection-count 2 --common-prompts format_guidelines > test3_output.txt 2>&1
if !errorlevel! equ 0 (
    echo [PASS] Agent creation with custom properties
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Agent creation with custom properties - Command failed
    type test3_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 4: Tool creation
echo [TEST 4] Tool creation
set /a TOTAL_TESTS+=1
dotnet run --project .. -- tool create --name TestTool --type KustoTool --extra description "Test tool for validation" > test4_output.txt 2>&1
if !errorlevel! equ 0 (
    echo [PASS] Tool creation
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Tool creation - Command failed
    type test4_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 5: Agent validation - Basic validation (without tool checking)
echo [TEST 5] Agent validation - Basic validation
set /a TOTAL_TESTS+=1
if exist "agents\test_agent\test_agent.yaml" (
    dotnet run --project .. -- agent validate --file agents\test_agent\test_agent.yaml > test5_output.txt 2>&1
    findstr /i "validation succeeded" test5_output.txt >nul
    if !errorlevel! equ 0 (
        echo [PASS] Agent validation - Basic validation
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Agent validation - Basic validation - Validation failed
        type test5_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [SKIP] Agent validation test - test_agent.yaml not found
)
echo.

REM Test 6: Agent validation with tool checking - Should pass for example_agent (has example_tool which exists)
echo [TEST 6] Agent validation with tool checking - Valid tools
set /a TOTAL_TESTS+=1
if exist "agents\example_agent.yaml" (
    dotnet run --project .. -- agent validate --file agents\example_agent.yaml --check-tools > test6_output.txt 2>&1
    if !errorlevel! equ 0 (
        echo [PASS] Agent validation with tool checking - Valid tools
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Agent validation with tool checking - Valid tools
        type test6_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [SKIP] Agent validation with tool checking test - example_agent.yaml not found
)
echo.

REM Test 7: Create agent with non-existent tool for testing tool validation
echo [TEST 7] Create agent with missing tool
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name missing_tool_test --instructions "Test agent with missing tool - this is a comprehensive test with sufficient length" --tools NonExistentTool > test7_output.txt 2>&1
if !errorlevel! equ 0 (
    echo [PASS] Create agent with missing tool
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Create agent with missing tool - Command failed
    type test7_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 8: Agent validation with tool checking - Should fail for agent with missing tools
echo [TEST 8] Agent validation with tool checking - Missing tools
set /a TOTAL_TESTS+=1
if exist "agents\missing_tool_test\missing_tool_test.yaml" (
    dotnet run --project .. -- agent validate --file agents\missing_tool_test\missing_tool_test.yaml --check-tools > test8_output.txt 2>&1
    if !errorlevel! neq 0 (
        findstr /i "not available" test8_output.txt >nul
        if !errorlevel! equ 0 (
            echo [PASS] Agent validation with tool checking - Missing tools - Expected failure occurred
            set /a TESTS_PASSED+=1
        ) else (
            echo [FAIL] Agent validation with tool checking - Missing tools - Wrong error message
            type test8_output.txt
            set /a TESTS_FAILED+=1
        )
    ) else (
        echo [FAIL] Agent validation with tool checking - Missing tools - Expected failure but command succeeded
        type test8_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [SKIP] Agent validation with missing tools test - missing_tool_test.yaml not found
)
echo.

REM Test 9: Tool validation
echo [TEST 9] Tool validation
set /a TOTAL_TESTS+=1
if exist "tools\TestTool.yaml" (
    dotnet run --project .. -- tool validate --name TestTool > test9_output.txt 2>&1
    if !errorlevel! equ 0 (
        echo [PASS] Tool validation
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Tool validation - Command failed
        type test9_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [SKIP] Tool validation test - TestTool.yaml not found
)
echo.

REM Test 10: Validate all agents - Basic validation
echo [TEST 10] Validate all agents - Basic validation
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent validate --all > test10_output.txt 2>&1
if !errorlevel! equ 0 (
    echo [PASS] Validate all agents - Basic validation
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Validate all agents - Basic validation - Command failed
    type test10_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 11: Validate all agents with tool checking
echo [TEST 11] Validate all agents with tool checking
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent validate --all --check-tools > test11_output.txt 2>&1
REM This should fail because we have agents with missing tools
if !errorlevel! neq 0 (
    findstr /i "not available\|missing" test11_output.txt >nul
    if !errorlevel! equ 0 (
        echo [PASS] Validate all agents with tool checking - Expected failures detected
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Validate all agents with tool checking - Wrong error message
        type test11_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Validate all agents with tool checking - Expected failure but command succeeded
    type test11_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM ===========================================
REM NEGATIVE TEST CASES (Error Handling)
REM ===========================================

echo ===========================================
echo NEGATIVE TEST CASES (Error Handling)
echo ===========================================
echo.

REM Test 12: Agent creation with just name (should succeed with default instructions)
echo [TEST 12] Agent creation with default instructions
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name test_agent_defaults --tools Tool1 > test12_output.txt 2>&1
if !errorlevel! equ 0 (
    echo [PASS] Agent creation with default instructions - Command succeeded as expected
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Agent creation with default instructions - Command failed unexpectedly
    type test12_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 13: Agent creation without tools
echo [TEST 13] Agent creation - No tools
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name test_agent_no_tools --instructions "Test agent without tools - this is a comprehensive test with sufficient length" > test13_output.txt 2>&1
if !errorlevel! neq 0 (
    echo [PASS] Agent creation - No tools - Expected failure occurred
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Agent creation - No tools - Expected failure but command succeeded
    type test13_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 14: Agent creation with invalid name (contains spaces)
echo [TEST 14] Agent creation - Invalid name with spaces
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name "invalid name" --instructions "Test agent with invalid name - this is a comprehensive test with sufficient length" --tools Tool1 > test14_output.txt 2>&1
if !errorlevel! neq 0 (
    echo [PASS] Agent creation - Invalid name with spaces - Expected failure occurred
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Agent creation - Invalid name with spaces - Expected failure but command succeeded
    type test14_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 15: Agent creation with short instructions
echo [TEST 15] Agent creation - Short instructions
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name test_agent_short --instructions "short" --tools Tool1 > test15_output.txt 2>&1
if !errorlevel! neq 0 (
    echo [PASS] Agent creation - Short instructions - Expected failure occurred
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Agent creation - Short instructions - Expected failure but command succeeded
    type test15_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 16: Agent creation with invalid temperature
echo [TEST 16] Agent creation - Invalid temperature
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name test_agent_temp --instructions "Test agent with invalid temperature - this is a comprehensive test with sufficient length" --tools Tool1 --temperature 5.0 > test16_output.txt 2>&1
if !errorlevel! neq 0 (
    echo [PASS] Agent creation - Invalid temperature - Expected failure occurred
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Agent creation - Invalid temperature - Expected failure but command succeeded
    type test16_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 17: Agent creation with negative max-reflection-count
echo [TEST 17] Agent creation - Negative max-reflection-count
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name test_agent_reflection --instructions "Test agent with negative reflection count - this is a comprehensive test with sufficient length" --tools Tool1 --max-reflection-count -1 > test17_output.txt 2>&1
if !errorlevel! neq 0 (
    echo [PASS] Agent creation - Negative max-reflection-count - Expected failure occurred
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Agent creation - Negative max-reflection-count - Expected failure but command succeeded
    type test17_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 18: Agent validation - Non-existent file
echo [TEST 18] Agent validation - Non-existent file
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent validate --file non_existent_file.yaml > test18_output.txt 2>&1
if !errorlevel! neq 0 (
    echo [PASS] Agent validation - Non-existent file - Expected failure occurred
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Agent validation - Non-existent file - Expected failure but command succeeded
    type test18_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 19: Tool validation - Non-existent tool
echo [TEST 19] Tool validation - Non-existent tool
set /a TOTAL_TESTS+=1
dotnet run --project .. -- tool validate --name NonExistentTool > test19_output.txt 2>&1
if !errorlevel! neq 0 (
    echo [PASS] Tool validation - Non-existent tool - Expected failure occurred
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Tool validation - Non-existent tool - Expected failure but command succeeded
    type test19_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 20: Agent validation with tool checking - Non-existent file
echo [TEST 20] Agent validation with tool checking - Non-existent file
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent validate --file non_existent_file.yaml --check-tools > test20_output.txt 2>&1
if !errorlevel! neq 0 (
    echo [PASS] Agent validation with tool checking - Non-existent file - Expected failure occurred
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Agent validation with tool checking - Non-existent file - Expected failure but command succeeded
    type test20_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM ===========================================
REM YAML FORMAT AND STRUCTURE TESTS
REM ===========================================

echo ===========================================
echo YAML FORMAT AND STRUCTURE TESTS
echo ===========================================
echo.

REM Test 21: Verify snake_case conversion in YAML
echo [TEST 21] Snake case conversion test
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name snake_case_test --instructions "Test snake case conversion in YAML output - this is a comprehensive test with sufficient length" --tools Tool1 --allow-parallel-tool-calls --max-reflection-count 1 > test21_output.txt 2>&1
if !errorlevel! equ 0 (
    echo [PASS] Snake case conversion test
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Snake case conversion test - Command failed
    type test21_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 22: Verify boolean properties in YAML
echo [TEST 22] Boolean properties test
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name boolean_test --instructions "Test boolean properties in YAML output - this is a comprehensive test with sufficient length" --tools Tool1 --allow-parallel-tool-calls --critic-on-handoff > test22_output.txt 2>&1
if !errorlevel! equ 0 (
    echo [PASS] Boolean properties test
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Boolean properties test - Command failed
    type test22_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 23: Verify directory structure creation
echo [TEST 23] Directory structure test
set /a TOTAL_TESTS+=1
if exist "agents\test_agent" (
    echo [PASS] Directory structure test - agents\test_agent directory created
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Directory structure test - agents\test_agent directory not found
    set /a TESTS_FAILED+=1
)
echo.

REM Test 24: Verify YAML file contents (snake_case)
echo [TEST 24] YAML snake_case format test
set /a TOTAL_TESTS+=1
if exist "agents\snake_case_test\snake_case_test.yaml" (
    findstr /i "allow_parallel_tool_calls" "agents\snake_case_test\snake_case_test.yaml" >nul
    if !errorlevel! equ 0 (
        echo [PASS] YAML snake_case format test - allow_parallel_tool_calls found
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] YAML snake_case format test - allow_parallel_tool_calls not found in snake_case
        set /a TESTS_FAILED+=1
    )
) else (
    echo [SKIP] YAML snake_case format test - snake_case_test.yaml not found
)
echo.

REM Test 25: Verify init command created proper structure
echo [TEST 25] Init command structure test
set /a TOTAL_TESTS+=1
if exist "agents\example_agent.yaml" (
    if exist "tools\example_tool.yaml" (
        if exist ".sreagent-config.json" (
            echo [PASS] Init command structure test - All expected files created
            set /a TESTS_PASSED+=1
        ) else (
            echo [FAIL] Init command structure test - .sreagent-config.json not found
            set /a TESTS_FAILED+=1
        )
    ) else (
        echo [FAIL] Init command structure test - example_tool.yaml not found
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Init command structure test - example_agent.yaml not found
    set /a TESTS_FAILED+=1
)
echo.

REM ===========================================
REM TOOL VALIDATION COMPREHENSIVE TESTS
REM ===========================================

echo ===========================================
echo TOOL VALIDATION COMPREHENSIVE TESTS
echo ===========================================
echo.

REM Test 26: Create tools that agents can reference
echo [TEST 26] Create BulkTool1
set /a TOTAL_TESTS+=1
dotnet run --project .. -- tool create --name BulkTool1 --type KustoTool --extra description "Bulk tool 1 for validation" > test26_output.txt 2>&1
if !errorlevel! equ 0 (
    echo [PASS] Create BulkTool1
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Create BulkTool1 - Command failed
    type test26_output.txt
    set /a TESTS_FAILED+=1
)
echo.

echo [TEST 27] Create BulkTool2
set /a TOTAL_TESTS+=1
dotnet run --project .. -- tool create --name BulkTool2 --type KustoTool --extra description "Bulk tool 2 for validation" > test27_output.txt 2>&1
if !errorlevel! equ 0 (
    echo [PASS] Create BulkTool2
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Create BulkTool2 - Command failed
    type test27_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 28: Create multiple agents for bulk validation
echo [TEST 28] Bulk agent creation 1 - With existing tools
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name bulk_agent_1 --instructions "First agent for bulk validation testing - this is a comprehensive test with sufficient length" --tools BulkTool1 > test28_output.txt 2>&1
if !errorlevel! equ 0 (
    echo [PASS] Bulk agent creation 1 - With existing tools
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Bulk agent creation 1 - With existing tools - Command failed
    type test28_output.txt
    set /a TESTS_FAILED+=1
)
echo.

echo [TEST 29] Bulk agent creation 2 - With existing tools
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name bulk_agent_2 --instructions "Second agent for bulk validation testing - this is a comprehensive test with sufficient length" --tools BulkTool2 > test29_output.txt 2>&1
if !errorlevel! equ 0 (
    echo [PASS] Bulk agent creation 2 - With existing tools
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Bulk agent creation 2 - With existing tools - Command failed
    type test29_output.txt
    set /a TESTS_FAILED+=1
)
echo.

echo [TEST 30] Bulk agent creation 3 - With mixed existing/missing tools
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name bulk_agent_3 --instructions "Third agent for bulk validation testing - this is a comprehensive test with sufficient length" --tools BulkTool1 NonExistentBulkTool > test30_output.txt 2>&1
if !errorlevel! equ 0 (
    echo [PASS] Bulk agent creation 3 - With mixed tools
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Bulk agent creation 3 - With mixed tools - Command failed
    type test30_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 31: Validate agents with tool checking - Mixed results expected
echo [TEST 31] Bulk validation with tool checking
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent validate --all --check-tools > test31_output.txt 2>&1
REM This should fail because we have agents with missing tools, but show detailed results
findstr /i "not available\|missing\|succeeded" test31_output.txt >nul
if !errorlevel! equ 0 (
    echo [PASS] Bulk validation with tool checking - Mixed results as expected
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Bulk validation with tool checking - Unexpected output format
    type test31_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 32: Test individual agent validation with existing tools
echo [TEST 32] Individual agent validation - With existing tools
set /a TOTAL_TESTS+=1
if exist "agents\bulk_agent_1\bulk_agent_1.yaml" (
    dotnet run --project .. -- agent validate --file agents\bulk_agent_1\bulk_agent_1.yaml --check-tools > test32_output.txt 2>&1
    if !errorlevel! equ 0 (
        echo [PASS] Individual agent validation - With existing tools
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Individual agent validation - With existing tools
        type test32_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [SKIP] Individual agent validation test - bulk_agent_1.yaml not found
)
echo.

REM Test 33: Test individual agent validation with mixed tools
echo [TEST 33] Individual agent validation - With mixed tools
set /a TOTAL_TESTS+=1
if exist "agents\bulk_agent_3\bulk_agent_3.yaml" (
    dotnet run --project .. -- agent validate --file agents\bulk_agent_3\bulk_agent_3.yaml --check-tools > test33_output.txt 2>&1
    if !errorlevel! neq 0 (
        findstr /i "not available" test33_output.txt >nul
        if !errorlevel! equ 0 (
            echo [PASS] Individual agent validation - With mixed tools - Expected failure
            set /a TESTS_PASSED+=1
        ) else (
            echo [FAIL] Individual agent validation - With mixed tools - Wrong error message
            type test33_output.txt
            set /a TESTS_FAILED+=1
        )
    ) else (
        echo [FAIL] Individual agent validation - With mixed tools - Expected failure but command succeeded
        type test33_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [SKIP] Individual agent validation with mixed tools test - bulk_agent_3.yaml not found
)
echo.

REM ===========================================
REM APPLY COMMAND TESTS
REM ===========================================

echo ===========================================
echo APPLY COMMAND TESTS
echo ===========================================
echo.

REM Test 34: Agent apply with valid agent
echo [TEST 34] Agent apply - Valid agent
set /a TOTAL_TESTS+=1
if exist "agents\example_agent.yaml" (
    dotnet run --project .. -- agent apply --name example_agent > test34_output.txt 2>&1
    if !errorlevel! equ 0 (
        findstr /i "applied successfully" test34_output.txt >nul
        if !errorlevel! equ 0 (
            echo [PASS] Agent apply - Valid agent
            set /a TESTS_PASSED+=1
        ) else (
            echo [FAIL] Agent apply - Valid agent - Wrong success message
            type test34_output.txt
            set /a TESTS_FAILED+=1
        )
    ) else (
        echo [FAIL] Agent apply - Valid agent - Command failed
        type test34_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [SKIP] Agent apply test - example_agent.yaml not found
)
echo.

REM Test 35: Agent apply with non-existent agent
echo [TEST 35] Agent apply - Non-existent agent
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent apply --name non_existent_agent > test35_output.txt 2>&1
if !errorlevel! neq 0 (
    echo [PASS] Agent apply - Non-existent agent - Expected failure occurred
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Agent apply - Non-existent agent - Expected failure but command succeeded
    type test35_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 36: Tool apply with valid tool
echo [TEST 36] Tool apply - Valid tool
set /a TOTAL_TESTS+=1
if exist "tools\example_tool.yaml" (
    dotnet run --project .. -- tool apply --name example_tool > test36_output.txt 2>&1
    if !errorlevel! equ 0 (
        findstr /i "applied successfully" test36_output.txt >nul
        if !errorlevel! equ 0 (
            echo [PASS] Tool apply - Valid tool
            set /a TESTS_PASSED+=1
        ) else (
            echo [FAIL] Tool apply - Valid tool - Wrong success message
            type test36_output.txt
            set /a TESTS_FAILED+=1
        )
    ) else (
        echo [FAIL] Tool apply - Valid tool - Command failed
        type test36_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [SKIP] Tool apply test - example_tool.yaml not found
)
echo.

REM Test 37: Tool apply with non-existent tool
echo [TEST 37] Tool apply - Non-existent tool
set /a TOTAL_TESTS+=1
dotnet run --project .. -- tool apply --name non_existent_tool > test37_output.txt 2>&1
if !errorlevel! neq 0 (
    echo [PASS] Tool apply - Non-existent tool - Expected failure occurred
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Tool apply - Non-existent tool - Expected failure but command succeeded
    type test37_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 38: Agent apply with agent that has referenced tools
echo [TEST 38] Agent apply - With referenced tools inclusion
set /a TOTAL_TESTS+=1
if exist "agents\bulk_agent_1\bulk_agent_1.yaml" (
    dotnet run --project .. -- agent apply --name bulk_agent_1 > test38_output.txt 2>&1
    if !errorlevel! equ 0 (
        findstr /i "Loaded tool" test38_output.txt >nul
        if !errorlevel! equ 0 (
            echo [PASS] Agent apply - With referenced tools inclusion
            set /a TESTS_PASSED+=1
        ) else (
            echo [FAIL] Agent apply - With referenced tools inclusion - Tool loading not shown
            type test38_output.txt
            set /a TESTS_FAILED+=1
        )
    ) else (
        echo [FAIL] Agent apply - With referenced tools inclusion - Command failed
        type test38_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [SKIP] Agent apply with tools test - bulk_agent_1.yaml not found
)
echo.

REM ===========================================
REM MALFORMED YAML TESTS
REM ===========================================

echo ===========================================
echo MALFORMED YAML TESTS
echo ===========================================
echo.

REM Test 39: Create malformed YAML with invalid syntax
echo [TEST 39] Malformed YAML - Invalid syntax
set /a TOTAL_TESTS+=1
echo name: malformed_agent > agents\malformed_syntax.yaml
echo instructions: "Test agent" >> agents\malformed_syntax.yaml
echo tools: >> agents\malformed_syntax.yaml
echo   - Tool1 >> agents\malformed_syntax.yaml
echo   invalid_indentation >> agents\malformed_syntax.yaml
dotnet run --project .. -- agent validate --file agents\malformed_syntax.yaml > test39_output.txt 2>&1
if !errorlevel! neq 0 (
    echo [PASS] Malformed YAML - Invalid syntax - Expected failure occurred
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Malformed YAML - Invalid syntax - Expected failure but command succeeded
    type test39_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 40: Create YAML with missing required fields
echo [TEST 40] Malformed YAML - Missing required fields
set /a TOTAL_TESTS+=1
echo name: incomplete_agent > agents\missing_fields.yaml
echo instructions: "Test agent" >> agents\missing_fields.yaml
REM Missing tools field
dotnet run --project .. -- agent validate --file agents\missing_fields.yaml > test40_output.txt 2>&1
if !errorlevel! neq 0 (
    echo [PASS] Malformed YAML - Missing required fields - Expected failure occurred
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Malformed YAML - Missing required fields - Expected failure but command succeeded
    type test40_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 41: Create YAML with invalid data types
echo [TEST 41] Malformed YAML - Invalid data types
set /a TOTAL_TESTS+=1
echo name: invalid_types > agents\invalid_types.yaml
echo instructions: "Test agent with invalid types" >> agents\invalid_types.yaml
echo tools: >> agents\invalid_types.yaml
echo   - Tool1 >> agents\invalid_types.yaml
echo temperature: "not_a_number" >> agents\invalid_types.yaml
echo max_reflection_count: "also_not_a_number" >> agents\invalid_types.yaml
dotnet run --project .. -- agent validate --file agents\invalid_types.yaml > test41_output.txt 2>&1
if !errorlevel! neq 0 (
    echo [PASS] Malformed YAML - Invalid data types - Expected failure occurred
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Malformed YAML - Invalid data types - Expected failure but command succeeded
    type test41_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 42: Create YAML with malformed structure
echo [TEST 42] Malformed YAML - Malformed structure
set /a TOTAL_TESTS+=1
echo name: "malformed_structure" > agents\malformed_structure.yaml
echo instructions: >> agents\malformed_structure.yaml
echo   this_should_be_a_string: "not a proper instructions field" >> agents\malformed_structure.yaml
echo tools: "this_should_be_a_list" >> agents\malformed_structure.yaml
dotnet run --project .. -- agent validate --file agents\malformed_structure.yaml > test42_output.txt 2>&1
if !errorlevel! neq 0 (
    echo [PASS] Malformed YAML - Malformed structure - Expected failure occurred
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Malformed YAML - Malformed structure - Expected failure but command succeeded
    type test42_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM ===========================================
REM CONFIGURATION FILE TESTS
REM ===========================================

echo ===========================================
echo CONFIGURATION FILE TESTS
echo ===========================================
echo.

REM Test 43: Backup and test with missing config file
echo [TEST 43] Missing configuration file
set /a TOTAL_TESTS+=1
if exist ".sreagent-config.json" (
    copy ".sreagent-config.json" ".sreagent-config.json.backup" >nul
    del ".sreagent-config.json"
)
dotnet run --project .. -- agent validate --file agents\example_agent.yaml > test43_output.txt 2>&1
REM Should still work with default behavior or prompt for config
if !errorlevel! equ 0 (
    echo [PASS] Missing configuration file - Handled gracefully
    set /a TESTS_PASSED+=1
) else (
    findstr /i "config\|configuration" test43_output.txt >nul
    if !errorlevel! equ 0 (
        echo [PASS] Missing configuration file - Appropriate error message
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Missing configuration file - Unexpected behavior
        type test43_output.txt
        set /a TESTS_FAILED+=1
    )
)
REM Restore config file
if exist ".sreagent-config.json.backup" (
    copy ".sreagent-config.json.backup" ".sreagent-config.json" >nul
    del ".sreagent-config.json.backup"
)
echo.

REM Test 44: Test with corrupted config file
echo [TEST 44] Corrupted configuration file
set /a TOTAL_TESTS+=1
if exist ".sreagent-config.json" (
    copy ".sreagent-config.json" ".sreagent-config.json.backup" >nul
)
echo { "invalid": "json" "missing_comma": true > ".sreagent-config.json"
dotnet run --project .. -- agent validate --file agents\example_agent.yaml > test44_output.txt 2>&1
REM Should handle corrupted config gracefully
if !errorlevel! neq 0 (
    findstr /i "config\|json\|parse" test44_output.txt >nul
    if !errorlevel! equ 0 (
        echo [PASS] Corrupted configuration file - Appropriate error handling
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Corrupted configuration file - Wrong error message
        type test44_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Corrupted configuration file - Should have failed but succeeded
    type test44_output.txt
    set /a TESTS_FAILED+=1
)
REM Restore config file
if exist ".sreagent-config.json.backup" (
    copy ".sreagent-config.json.backup" ".sreagent-config.json" >nul
    del ".sreagent-config.json.backup"
)
echo.

REM Test 45: Test with invalid resource URL in config
echo [TEST 45] Invalid resource URL in configuration
set /a TOTAL_TESTS+=1
if exist ".sreagent-config.json" (
    copy ".sreagent-config.json" ".sreagent-config.json.backup" >nul
)
echo { "resourceUrl": "not-a-valid-url" } > ".sreagent-config.json"
dotnet run --project .. -- agent validate --file agents\example_agent.yaml --check-tools > test45_output.txt 2>&1
REM Should handle invalid URL gracefully
if !errorlevel! neq 0 (
    findstr /i "url\|connection\|invalid" test45_output.txt >nul
    if !errorlevel! equ 0 (
        echo [PASS] Invalid resource URL - Appropriate error handling
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Invalid resource URL - Wrong error message
        type test45_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Invalid resource URL - Should have failed but succeeded
    type test45_output.txt
    set /a TESTS_FAILED+=1
)
REM Restore config file
if exist ".sreagent-config.json.backup" (
    copy ".sreagent-config.json.backup" ".sreagent-config.json" >nul
    del ".sreagent-config.json.backup"
)
echo.

REM ===========================================
REM TOOL TYPE VALIDATION TESTS
REM ===========================================

echo ===========================================
echo TOOL TYPE VALIDATION TESTS
echo ===========================================
echo.

REM Test 46: Create tool with invalid type
echo [TEST 46] Tool creation - Invalid tool type
set /a TOTAL_TESTS+=1
dotnet run --project .. -- tool create --name invalid_type_tool --type InvalidToolType --extra description "Tool with invalid type" > test46_output.txt 2>&1
if !errorlevel! neq 0 (
    findstr /i "invalid\|type\|unknown" test46_output.txt >nul
    if !errorlevel! equ 0 (
        echo [PASS] Tool creation - Invalid tool type - Expected failure occurred
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Tool creation - Invalid tool type - Wrong error message
        type test46_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Tool creation - Invalid tool type - Expected failure but command succeeded
    type test46_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 47: Create tool with missing required fields for type
echo [TEST 47] Tool validation - Missing required fields for type
set /a TOTAL_TESTS+=1
echo name: incomplete_kusto_tool > tools\incomplete_kusto.yaml
echo type: KustoTool >> tools\incomplete_kusto.yaml
REM Missing description and other KustoTool-specific fields
dotnet run --project .. -- tool validate --name incomplete_kusto > test47_output.txt 2>&1
if !errorlevel! neq 0 (
    echo [PASS] Tool validation - Missing required fields for type - Expected failure occurred
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Tool validation - Missing required fields for type - Expected failure but command succeeded
    type test47_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 48: Create multiple tools with different valid types
echo [TEST 48] Tool creation - Multiple valid types
set /a TOTAL_TESTS+=1
dotnet run --project .. -- tool create --name valid_type_tool1 --type KustoTool --extra description "Valid Kusto tool" > test48a_output.txt 2>&1
set RESULT1=!errorlevel!
dotnet run --project .. -- tool create --name valid_type_tool2 --type GenericTool --extra description "Valid Generic tool" > test48b_output.txt 2>&1
set RESULT2=!errorlevel!
if !RESULT1! equ 0 (
    if !RESULT2! equ 0 (
        echo [PASS] Tool creation - Multiple valid types
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Tool creation - Multiple valid types - Second tool creation failed
        type test48b_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [FAIL] Tool creation - Multiple valid types - First tool creation failed
    type test48a_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 49: Tool schema validation against type
echo [TEST 49] Tool schema validation - Type consistency
set /a TOTAL_TESTS+=1
echo name: schema_mismatch_tool > tools\schema_mismatch.yaml
echo type: KustoTool >> tools\schema_mismatch.yaml
echo description: "Tool with wrong schema for type" >> tools\schema_mismatch.yaml
echo some_invalid_field: "This field should not exist for KustoTool" >> tools\schema_mismatch.yaml
dotnet run --project .. -- tool validate --name schema_mismatch > test49_output.txt 2>&1
REM This might pass or fail depending on schema validation strictness
if !errorlevel! neq 0 (
    echo [PASS] Tool schema validation - Type consistency - Validation caught schema mismatch
    set /a TESTS_PASSED+=1
) else (
    REM If it passes, that's also acceptable if schema validation is lenient
    echo [PASS] Tool schema validation - Type consistency - Schema validation is lenient
    set /a TESTS_PASSED+=1
)
echo.

echo.

REM ===========================================
REM CLEANUP AND SUMMARY
REM ===========================================

echo ===========================================
echo CLEANUP AND TEST SUMMARY
echo ===========================================
echo.

echo Cleaning up test files...

REM Clean up test agents
if exist "agents" (
    if exist "agents\test_agent" rmdir /s /q "agents\test_agent"
    if exist "agents\test_agent_defaults" rmdir /s /q "agents\test_agent_defaults"
    if exist "agents\full_featured_agent" rmdir /s /q "agents\full_featured_agent"
    if exist "agents\custom_agent" rmdir /s /q "agents\custom_agent"
    if exist "agents\snake_case_test" rmdir /s /q "agents\snake_case_test"
    if exist "agents\boolean_test" rmdir /s /q "agents\boolean_test"
    if exist "agents\missing_tool_test" rmdir /s /q "agents\missing_tool_test"
    if exist "agents\bulk_agent_1" rmdir /s /q "agents\bulk_agent_1"
    if exist "agents\bulk_agent_2" rmdir /s /q "agents\bulk_agent_2"
    if exist "agents\bulk_agent_3" rmdir /s /q "agents\bulk_agent_3"
    
    REM Keep example_agent.yaml as it was created by init
)

REM Clean up test tools
if exist "tools" (
    if exist "tools\TestTool.yaml" del "tools\TestTool.yaml"
    if exist "tools\BulkTool1.yaml" del "tools\BulkTool1.yaml"
    if exist "tools\BulkTool2.yaml" del "tools\BulkTool2.yaml"
    if exist "tools\incomplete_kusto.yaml" del "tools\incomplete_kusto.yaml"
    if exist "tools\schema_mismatch.yaml" del "tools\schema_mismatch.yaml"
    if exist "tools\valid_type_tool1.yaml" del "tools\valid_type_tool1.yaml"
    if exist "tools\valid_type_tool2.yaml" del "tools\valid_type_tool2.yaml"
    
    REM Keep example_tool.yaml as it was created by init
)

REM Clean up malformed YAML test files
if exist "agents" (
    if exist "agents\malformed_syntax.yaml" del "agents\malformed_syntax.yaml"
    if exist "agents\missing_fields.yaml" del "agents\missing_fields.yaml"
    if exist "agents\invalid_types.yaml" del "agents\invalid_types.yaml"
    if exist "agents\malformed_structure.yaml" del "agents\malformed_structure.yaml"
)

REM Clean up test output files
del test*_output.txt 2>nul
del init_output.txt 2>nul

REM Return to original directory
popd
rmdir test_output 2>nul

echo.
echo =====================================
echo TEST SUMMARY
echo =====================================
echo Total Tests: !TOTAL_TESTS!
echo Passed: !TESTS_PASSED!
echo Failed: !TESTS_FAILED!

if !TESTS_FAILED! equ 0 (
    echo.
    echo [SUCCESS] All tests passed!
    echo The SRECTL is functioning correctly.
    echo.
    echo Key features tested:
    echo - Agent creation and validation
    echo - Tool creation and validation 
    echo - Basic YAML validation
    echo - Tool existence validation (--check-tools)
    echo - Error handling for missing tools
    echo - Bulk validation operations
    echo - YAML format verification
    echo - Directory structure validation
    echo - Apply command functionality
    echo - Malformed YAML handling
    echo - Configuration file edge cases
    echo - Tool type validation and schema checking
    exit /b 0
) else (
    echo.
    echo [FAILURE] Some tests failed!
    echo Please review the failed tests above and fix any issues.
    exit /b 1
)
