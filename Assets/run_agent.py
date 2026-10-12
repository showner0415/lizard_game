import os
from typing import TypedDict

# 각 AI 모델 라이브러리 불러오기
from langchain_google_genai import ChatGoogleGenerativeAI
from langchain_openai import ChatOpenAI
from langchain_anthropic import ChatAnthropic
from langgraph.graph import StateGraph, END

# ==========================================
# 1. API 키 설정 (실제 발급받은 키를 따옴표 안에 넣으세요)
# ==========================================
os.environ["GEMINI_API_KEY"] = "AQ.Ab8RN6LRR3z-cVtOMMFH9jXZPzcLS88Os5_YxMf-OiUwkAhNGw"
os.environ["OPENAI_API_KEY"] = "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"
os.environ["ANTHROPIC_API_KEY"] = "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"

# ==========================================
# 2. 각 역할에 맞는 AI 모델 선언
# ==========================================
# 기획 담당: Gemini 1.5 Flash (또는 gemini-2.0-flash)
gemini_planner = ChatGoogleGenerativeAI(
    model="gemini-3.8-flash",
    temperature=0.7,
    google_api_key=os.environ["GEMINI_API_KEY"]
)

# 아키텍처/설계 담당: GPT-4o
gpt_architect = ChatOpenAI(
    model="gpt-4o",
    temperature=0.3
)

# C# 코딩 구현 담당: Claude 3.5 Sonnet
claude_coder = ChatAnthropic(
    model="claude-opus-5-5"
)

# ==========================================
# 3. State (공유 기록지) 정의
# ==========================================
class MultiAgentState(TypedDict):
    user_idea: str       # 사용자가 처음 던진 아이디어
    plan_overview: str   # [Gemini] 초기 기획안
    tech_spec: str       # [GPT] 기술 아키텍처 및 클래스 설계
    final_code: str      # [Claude] 최종 완성된 유니티 C# 코드

# ==========================================
# 4. 각 AI 작업자(Node) 함수 정의
# ==========================================
def gemini_planning_node(state: MultiAgentState):
    """[Gemini] 아이디어를 받아 게임의 핵심 재미와 시스템 기획안을 작성합니다."""
    print("\n[Step 1: Gemini] 게임 시스템 기획 초안 작성 중...")
    
    prompt = f"""
    당신은 창의적인 인디 게임 기획자입니다.
    아래 아이디어를 보고, 플레이어의 핵심 루프와 시스템 규칙을 명확하게 3단계로 기획해 주세요.
    
    사용자 아이디어: {state['user_idea']}
    """
    response = gemini_planner.invoke(prompt)
    return {"plan_overview": response.content}

def gpt_architect_node(state: MultiAgentState):
    """[GPT] Gemini의 기획안을 분석하여 유니티 컴포넌트 구조와 변수, 메서드를 설계합니다."""
    print("\n[Step 2: GPT] C# 아키텍처 및 컴포넌트 구조 설계 중...")
    
    prompt = f"""
    당신은 유니티 테크니컬 아키텍트입니다.
    아래 기획안을 보고 유니티에서 구현할 때 필요한 스크립트 구조를 작성하세요.
    - 필요한 컴포넌트 및 의존성
    - 핵심 변수와 메서드 시그니처 명세
    - 불필요한 Update 호출을 막는 최적화 가이드
    
    기획안:
    {state['plan_overview']}
    """
    response = gpt_architect.invoke(prompt)
    return {"tech_spec": response.content}

def claude_coding_node(state: MultiAgentState):
    """[Claude] 기획안과 기술 설계를 바탕으로 실전 유니티 C# 스크립트를 작성합니다."""
    print("\n[Step 3: Claude] 완성형 C# 스크립트 작성 중...")
    
    prompt = f"""
    당신은 10년 차 시니어 유니티 C# 프로그래머입니다.
    제공된 기술 명세서를 바탕으로 즉시 복사해 유니티에 적용할 수 있는 완전한 MonoBehaviour 스크립트를 작성하세요.
    코드 내에 핵심 로직 주석을 포함하세요.
    
    기술 명세서:
    {state['tech_spec']}
    """
    response = claude_coder.invoke(prompt)
    return {"final_code": response.content}

# ==========================================
# 5. Graph (파이프라인) 조립
# ==========================================
workflow = StateGraph(MultiAgentState)

# 노드 등록
workflow.add_node("planner", gemini_planning_node)
workflow.add_node("architect", gpt_architect_node)
workflow.add_node("coder", claude_coding_node)

# 파이프라인 흐름 연결: 시작 -> Gemini -> GPT -> Claude -> 종료
workflow.set_entry_point("planner")
workflow.add_edge("planner", "architect")
workflow.add_edge("architect", "coder")
workflow.add_edge("coder", END)

# 컴파일
app = workflow.compile()

# ==========================================
# 6. 파이프라인 가동
# ==========================================
if __name__ == "__main__":
    initial_request = {
        "user_idea": "플레이어가 몬스터와 충돌했을 때 HP 슬라이더가 부드럽게 깎이고 1초간 깜빡이며 무적 상태가 되는 시스템"
    }

    result = app.invoke(initial_request)

    print("\n" + "="*50)
    print("### 1. [Gemini] 기획안 ###\n")
    print(result["plan_overview"])
    
    print("\n" + "="*50)
    print("### 2. [GPT] 아키텍처 명세서 ###\n")
    print(result["tech_spec"])
    
    print("\n" + "="*50)
    print("### 3. [Claude] 최종 C# 완성 코드 ###\n")
    print(result["final_code"])