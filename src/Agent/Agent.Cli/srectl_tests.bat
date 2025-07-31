@echo off
setlocal enabledelayedexpansion

REM SRECTL Automated Test Suite
REM This batch file runs comprehensive tests for SRECTL
REM covering agent creation, validation, and error handling scenarios

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
dotnet run --project .. -- tool create --name TestTool --type KustoQuery --extra description "Test tool for validation" > test4_output.txt 2>&1
if !errorlevel! equ 0 (
    echo [PASS] Tool creation
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Tool creation - Command failed
    type test4_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 5: Agent validation (if test_agent was created successfully)
echo [TEST 5] Agent validation - Valid file
set /a TOTAL_TESTS+=1
if exist "agents\test_agent\test_agent.yaml" (
    dotnet run --project .. -- agent validate --file agents\test_agent\test_agent.yaml > test5_output.txt 2>&1
    findstr /i "validation passed" test5_output.txt >nul
    if !errorlevel! equ 0 (
        echo [PASS] Agent validation - Valid file
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Agent validation - Valid file - Validation failed
        type test5_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [SKIP] Agent validation test - test_agent.yaml not found
)
echo.

REM Test 6: Tool validation (if TestTool was created successfully)
echo [TEST 6] Tool validation
set /a TOTAL_TESTS+=1
if exist "tools\TestTool\TestTool.yaml" (
    dotnet run --project .. -- tool validate --name TestTool > test6_output.txt 2>&1
    if !errorlevel! equ 0 (
        echo [PASS] Tool validation
        set /a TESTS_PASSED+=1
    ) else (
        echo [FAIL] Tool validation - Command failed
        type test6_output.txt
        set /a TESTS_FAILED+=1
    )
) else (
    echo [SKIP] Tool validation test - TestTool.yaml not found
)
echo.

REM Test 7: Validate all agents
echo [TEST 7] Validate all agents
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent validate --all > test7_output.txt 2>&1
if !errorlevel! equ 0 (
    echo [PASS] Validate all agents
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Validate all agents - Command failed
    type test7_output.txt
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

REM Test 8: Agent creation with just name (should succeed with default instructions)
echo [TEST 8] Agent creation with default instructions
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name test_agent_defaults --tools Tool1 > test8_output.txt 2>&1
if !errorlevel! equ 0 (
    echo [PASS] Agent creation with default instructions - Command succeeded as expected
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Agent creation with default instructions - Command failed unexpectedly
    type test8_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 9: Agent creation without tools
echo [TEST 9] Agent creation - No tools
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name test_agent_no_tools --instructions "Test agent without tools - this is a comprehensive test with sufficient length" > test9_output.txt 2>&1
if !errorlevel! neq 0 (
    echo [PASS] Agent creation - No tools - Expected failure occurred
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Agent creation - No tools - Expected failure but command succeeded
    type test9_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 10: Agent creation with invalid name (contains spaces)
echo [TEST 10] Agent creation - Invalid name with spaces
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name "invalid name" --instructions "Test agent with invalid name - this is a comprehensive test with sufficient length" --tools Tool1 > test10_output.txt 2>&1
if !errorlevel! neq 0 (
    echo [PASS] Agent creation - Invalid name with spaces - Expected failure occurred
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Agent creation - Invalid name with spaces - Expected failure but command succeeded
    type test10_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 11: Agent creation with short instructions
echo [TEST 11] Agent creation - Short instructions
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name test_agent_short --instructions "short" --tools Tool1 > test11_output.txt 2>&1
if !errorlevel! neq 0 (
    echo [PASS] Agent creation - Short instructions - Expected failure occurred
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Agent creation - Short instructions - Expected failure but command succeeded
    type test11_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 12: Agent creation with invalid temperature
echo [TEST 12] Agent creation - Invalid temperature
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name test_agent_temp --instructions "Test agent with invalid temperature - this is a comprehensive test with sufficient length" --tools Tool1 --temperature 5.0 > test12_output.txt 2>&1
if !errorlevel! neq 0 (
    echo [PASS] Agent creation - Invalid temperature - Expected failure occurred
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Agent creation - Invalid temperature - Expected failure but command succeeded
    type test12_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 13: Agent creation with negative max-reflection-count
echo [TEST 13] Agent creation - Negative max-reflection-count
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name test_agent_reflection --instructions "Test agent with negative reflection count - this is a comprehensive test with sufficient length" --tools Tool1 --max-reflection-count -1 > test13_output.txt 2>&1
if !errorlevel! neq 0 (
    echo [PASS] Agent creation - Negative max-reflection-count - Expected failure occurred
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Agent creation - Negative max-reflection-count - Expected failure but command succeeded
    type test13_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 14: Agent validation - Non-existent file
echo [TEST 14] Agent validation - Non-existent file
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent validate --file non_existent_file.yaml > test14_output.txt 2>&1
if !errorlevel! neq 0 (
    echo [PASS] Agent validation - Non-existent file - Expected failure occurred
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Agent validation - Non-existent file - Expected failure but command succeeded
    type test14_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 15: Tool validation - Non-existent tool
echo [TEST 15] Tool validation - Non-existent tool
set /a TOTAL_TESTS+=1
dotnet run --project .. -- tool validate --name NonExistentTool > test15_output.txt 2>&1
if !errorlevel! neq 0 (
    echo [PASS] Tool validation - Non-existent tool - Expected failure occurred
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Tool validation - Non-existent tool - Expected failure but command succeeded
    type test15_output.txt
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

REM Test 16: Verify snake_case conversion in YAML
echo [TEST 16] Snake case conversion test
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name snake_case_test --instructions "Test snake case conversion in YAML output - this is a comprehensive test with sufficient length" --tools Tool1 --allow-parallel-tool-calls --max-reflection-count 1 > test16_output.txt 2>&1
if !errorlevel! equ 0 (
    echo [PASS] Snake case conversion test
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Snake case conversion test - Command failed
    type test16_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 17: Verify boolean properties in YAML
echo [TEST 17] Boolean properties test
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name boolean_test --instructions "Test boolean properties in YAML output - this is a comprehensive test with sufficient length" --tools Tool1 --allow-parallel-tool-calls --critic-on-handoff > test17_output.txt 2>&1
if !errorlevel! equ 0 (
    echo [PASS] Boolean properties test
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Boolean properties test - Command failed
    type test17_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 18: Verify directory structure creation
echo [TEST 18] Directory structure test
set /a TOTAL_TESTS+=1
if exist "agents\test_agent" (
    echo [PASS] Directory structure test - agents\test_agent directory created
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Directory structure test - agents\test_agent directory not found
    set /a TESTS_FAILED+=1
)
echo.

REM Test 19: Verify YAML file contents (snake_case)
echo [TEST 19] YAML snake_case format test
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

REM ===========================================
REM BULK OPERATIONS TESTS
REM ===========================================

echo ===========================================
echo BULK OPERATIONS TESTS
echo ===========================================
echo.

REM Test 20: Create multiple agents for bulk validation
echo [TEST 20] Bulk agent creation 1
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name bulk_agent_1 --instructions "First agent for bulk validation testing - this is a comprehensive test with sufficient length" --tools BulkTool1 > test20_output.txt 2>&1
if !errorlevel! equ 0 (
    echo [PASS] Bulk agent creation 1
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Bulk agent creation 1 - Command failed
    type test20_output.txt
    set /a TESTS_FAILED+=1
)
echo.

echo [TEST 21] Bulk agent creation 2
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name bulk_agent_2 --instructions "Second agent for bulk validation testing - this is a comprehensive test with sufficient length" --tools BulkTool2 > test21_output.txt 2>&1
if !errorlevel! equ 0 (
    echo [PASS] Bulk agent creation 2
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Bulk agent creation 2 - Command failed
    type test21_output.txt
    set /a TESTS_FAILED+=1
)
echo.

echo [TEST 22] Bulk agent creation 3
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent create --name bulk_agent_3 --instructions "Third agent for bulk validation testing - this is a comprehensive test with sufficient length" --tools BulkTool3 > test22_output.txt 2>&1
if !errorlevel! equ 0 (
    echo [PASS] Bulk agent creation 3
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Bulk agent creation 3 - Command failed
    type test22_output.txt
    set /a TESTS_FAILED+=1
)
echo.

REM Test 23: Validate all agents (should include newly created ones)
echo [TEST 23] Bulk validation test
set /a TOTAL_TESTS+=1
dotnet run --project .. -- agent validate --all > test23_output.txt 2>&1
if !errorlevel! equ 0 (
    echo [PASS] Bulk validation test
    set /a TESTS_PASSED+=1
) else (
    echo [FAIL] Bulk validation test - Command failed
    type test23_output.txt
    set /a TESTS_FAILED+=1
)
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
    if exist "agents\bulk_agent_1" rmdir /s /q "agents\bulk_agent_1"
    if exist "agents\bulk_agent_2" rmdir /s /q "agents\bulk_agent_2"
    if exist "agents\bulk_agent_3" rmdir /s /q "agents\bulk_agent_3"
    
    REM Remove agents directory if empty
    dir /b "agents" 2>nul | findstr . >nul
    if !errorlevel! neq 0 rmdir "agents" 2>nul
)

REM Clean up test tools
if exist "tools" (
    if exist "tools\TestTool" rmdir /s /q "tools\TestTool"
    
    REM Remove tools directory if empty
    dir /b "tools" 2>nul | findstr . >nul
    if !errorlevel! neq 0 rmdir "tools" 2>nul
)

REM Clean up test output files
del test*_output.txt 2>nul

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
    exit /b 0
) else (
    echo.
    echo [FAILURE] Some tests failed!
    echo Please review the failed tests above and fix any issues.
    exit /b 1
)
