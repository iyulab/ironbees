# GitHub Workflows

이 디렉터리에는 Ironbees 프로젝트의 CI/CD 파이프라인이 포함되어 있습니다.

## Workflows

### 1. CI - Build and Test (`ci.yml`)

**트리거**:
- `main`, `develop` 브랜치에 push
- `main`, `develop` 브랜치로 Pull Request
- 문서 파일(`.md`, `docs/**`) 변경 시 제외

**작업**:
- .NET 9.0 환경 설정
- 의존성 복원
- Release 빌드
- 전체 테스트 실행
- 테스트 결과 게시
- 샘플 프로젝트 빌드 검증

**상태 배지**:
```markdown
![CI](https://github.com/iyulab/ironbees/actions/workflows/ci.yml/badge.svg)
```

### 2. Publish to NuGet (`publish.yml`)

**자동 트리거**:
- `main` 브랜치에 `Directory.Build.props` 파일 변경 push

**수동 트리거**:
GitHub Actions 페이지에서 "Run workflow" 버튼 클릭

**수동 트리거 옵션**:
- `force_publish`: 버전 체크 우회하고 강제 배포 (기본: false)

**작업 흐름**:
1. 전체 빌드 및 테스트 실행
2. `Directory.Build.props`에서 버전 추출
3. NuGet.org에 해당 버전 존재 여부 확인
4. 새 버전이면 NuGet 패키지 생성
5. NuGet.org에 배포
6. GitHub Release 자동 생성

**환경 변수 필요**:
- `NUGET_API_KEY`: NuGet.org API 키 (GitHub Secrets에 저장)

## 사용 방법

### CI 자동 실행

Pull Request나 push를 하면 자동으로 실행됩니다.

```bash
git add .
git commit -m "feat: add new feature"
git push origin feature-branch
# → CI workflow 자동 실행
```

### NuGet 자동 배포

1. 버전 업데이트:
```bash
# Directory.Build.props에서 버전 변경
<Version>0.2.0</Version>
```

2. 커밋 및 푸시:
```bash
git add Directory.Build.props
git commit -m "chore: bump version to 0.2.0"
git push origin main
# → NuGet publish workflow 자동 실행
```

### NuGet 수동 배포 (강제)

GitHub Actions 페이지에서:

1. "Actions" 탭으로 이동
2. "Publish to NuGet" 워크플로우 선택
3. "Run workflow" 버튼 클릭
4. 옵션 선택:
   - ✅ `force_publish`: 버전 중복 무시하고 배포
5. "Run workflow" 실행

### 배포 문제 해결

**버전이 이미 존재하는 경우**:
```
⚠️ Both packages version 0.1.0 already exist on NuGet.org
```
→ `Directory.Build.props`에서 버전을 올리거나 `force_publish` 옵션 사용

**API 키 오류**:
```
error: Response status code does not indicate success: 401 (Unauthorized)
```
→ GitHub Secrets에 `NUGET_API_KEY` 등록 확인

## GitHub Secrets 설정

1. GitHub 저장소 → Settings → Secrets and variables → Actions
2. "New repository secret" 클릭
3. Name: `NUGET_API_KEY`
4. Value: [NuGet.org에서 발급받은 API 키]
5. "Add secret" 클릭

### NuGet API 키 발급

1. https://www.nuget.org 로그인
2. 프로필 → API Keys
3. "Create" 클릭
4. Key Name: `GitHub Actions - Ironbees`
5. Glob Pattern: `Ironbees.*`
6. "Create" 클릭
7. 생성된 키 복사하여 GitHub Secrets에 등록

## 환경 설정

### GitHub Environment (선택적)

보안 강화를 위해 환경을 설정할 수 있습니다:

1. GitHub 저장소 → Settings → Environments
2. "New environment" 클릭
3. Name: `nuget-production`
4. Protection rules 설정:
   - Required reviewers: 승인 필요한 사람 추가
   - Wait timer: 배포 전 대기 시간

## 워크플로우 상태 확인

### 실행 중인 워크플로우 보기

```bash
# GitHub CLI 사용
gh run list --workflow=ci.yml
gh run list --workflow=publish.yml

# 특정 실행 로그 보기
gh run view <run-id>
```

### 로컬에서 워크플로우 테스트

[act](https://github.com/nektos/act)를 사용하여 로컬에서 GitHub Actions를 테스트할 수 있습니다:

```bash
# act 설치
winget install nektos.act

# CI workflow 실행
act push

# Publish workflow 실행 (with secrets)
act workflow_dispatch -W .github/workflows/publish.yml --secret NUGET_API_KEY=<your-key>
```

## 배지 추가

README.md에 상태 배지 추가:

```markdown
[![CI](https://github.com/iyulab/ironbees/actions/workflows/ci.yml/badge.svg)](https://github.com/iyulab/ironbees/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Ironbees.Core)](https://www.nuget.org/packages/Ironbees.Core)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Ironbees.Core)](https://www.nuget.org/packages/Ironbees.Core)
```

## 문제 해결

### 워크플로우가 실행되지 않는 경우

1. `.github/workflows/` 디렉터리가 올바른 위치에 있는지 확인
2. YAML 문법 오류 확인: https://www.yamllint.com/
3. 브랜치 필터 확인 (main vs master)
4. path 필터 확인

### 테스트 실패

CI에서 테스트가 실패하면 배포가 중단됩니다. 로컬에서 먼저 테스트:

```bash
dotnet test --configuration Release --verbosity detailed
```

### 배포 실패 후 재시도

1. 실패 원인 수정
2. 동일한 커밋에 대해 수동 트리거 사용
3. `force_publish` 옵션 활성화

---

**Ironbees Workflows** - Automated CI/CD for .NET NuGet packages 🐝
