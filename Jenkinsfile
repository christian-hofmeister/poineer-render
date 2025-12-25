// Jenkins Declarative Pipeline for POIneer.Render
// - Builds & tests on 'develop'
// - Placeholder "deploy" on 'release/*'
// - Avoids heavy rendering on Jenkins
// - Uses repository script to ensure dotnet SDK if missing

pipeline {
  agent any

  options {
    ansiColor('xterm')
    timestamps()
    disableConcurrentBuilds()
    buildDiscarder(logRotator(numToKeepStr: '20', artifactNumToKeepStr: '10'))
    timeout(time: 30, unit: 'MINUTES')
  }

  environment {
    // Speed up dotnet CLI
    DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    NUGET_PACKAGES = "${WORKSPACE}/.nuget/packages"
    // Desired SDK version for the project; adjust if you bump SDK
    DOTNET_SDK_VERSION = '10.0.100'
    // Project paths
    RENDER_CSProj = 'src/POIneer.Render/POIneer.Render.csproj'
    PUBLISH_DIR = 'out/POIneer.Render'
    COVERAGE_MIN = '25'   // später z. B. auf 40/50/60 anheben
  }

  stages {

    stage('Checkout') {
      steps {
        checkout scm
        sh 'git --no-pager log -1 --pretty=fuller'
      }
    }

    stage('Debug dotnet') {
      steps {
        sh '''
          set -eux
          which -a dotnet || true
          echo "PATH=$PATH"
          echo "DOTNET_ROOT=${DOTNET_ROOT-}"
          dotnet --version || true
          dotnet --version
          dotnet --list-sdks
        '''
      }
    }

    stage('Restore') {
      steps {
        sh '''
          set -eux
          mkdir -p "${NUGET_PACKAGES}"
          dotnet restore "${RENDER_CSProj}" --nologo
        '''
      }
    }

    stage('Build') {
      steps {
        sh '''
          set -eux
          dotnet build "${RENDER_CSProj}" -c Release --no-restore --nologo
        '''
      }
    }

    stage('Test with Coverage'){
      steps {
        sh '''
          set -eux

          # Tests ausführen
          dotnet test POIneerRender.sln -c Release --nologo \
            --results-directory TestResults \
            --logger "junit;LogFilePath=TestResults/test-results.junit.xml" \
            /p:CollectCoverage=true \
            /p:CoverletOutputFormat=cobertura \
            /p:CoverletOutput=TestResults/Coverage/coverage

          echo "[diag] Coverage files:"
          find . -type f -name "coverage.cobertura.xml" -print

          echo "[diag] JUnit files:"
          find . -type f -name "test-results.junit.xml" -print

          echo "[diag] TRX files:"
          find . -type f -name "*.trx" -print
        '''
      }
      post {
        always {
          // ✅ JUnit korrekt einsammeln
          junit '**/TestResults/**/test-results.junit.xml'

          // ✅ Cobertura Coverage publizieren
          recordCoverage(
            tools: [[parser: 'COBERTURA', pattern: '**/TestResults/**/coverage.cobertura.xml']],
            sourceCodeRetention: 'LAST_BUILD',
            failOnError: true
          )

          // ✅ Manuelle Coverage-Schwelle
          script {
            def min = env.COVERAGE_MIN ? env.COVERAGE_MIN.toInteger() : 25

            def pct = sh(
              script: '''
                set -eu
                f="$(ls -1 **/TestResults/**/coverage.cobertura.xml | head -n1)"
                awk -F'"' "/^<coverage/ {for(i=1;i<=NF;i++){if(\\$i ~ /line-rate=/){print \\$(i+1); exit}}}" "$f" \
                  | awk '{printf("%.0f\\n", $1*100)}'
              ''',
              returnStdout: true
            ).trim()

            echo "Line coverage detected: ${pct}% (threshold: ${min}%)"

            if (pct.isInteger() && pct.toInteger() < min) {
              currentBuild.result = 'FAILURE'
              echo "❌ Coverage below threshold."
            } else {
              echo "✅ Coverage threshold met."
            }
          }
        }
      }
    }


    stage('Coverage Report (HTML)') {
      when { expression { return fileExists('TestResults/Coverage/coverage.cobertura.xml') } }
      steps {
        sh '''
          set -eux
          dotnet tool update -g dotnet-reportgenerator-globaltool || dotnet tool install -g dotnet-reportgenerator-globaltool
          export PATH="$HOME/.dotnet/tools:$PATH"
          reportgenerator \
            -reports:**/TestResults/**/coverage.cobertura.xml \
            -targetdir:CoverageReport \
            -reporttypes:Html
        '''
      }
      post {
        always {
          archiveArtifacts artifacts: 'CoverageReport/**', fingerprint: false
        }
      }
    }

    stage('Publish (App)') {
      steps {
        sh '''
          set -eux
          rm -rf "${PUBLISH_DIR}"
          dotnet publish "${RENDER_CSProj}" -c Release -o "${PUBLISH_DIR}" --no-build --nologo
          tar -C "${PUBLISH_DIR}" -czf "poineer-render_${BRANCH_NAME}.tar.gz" .
        '''
      }
      post {
        success {
          archiveArtifacts artifacts: 'poineer-render_*.tar.gz', fingerprint: true
        }
      }
    }

    stage('Deploy (placeholder)') {
      when {
        expression {
          // Run on branches like release/1.0.0, release/v0.3, etc.
          return env.BRANCH_NAME ==~ /release\/.+/
        }
      }
      steps {
        sh '''
          set -eux
          echo "[deploy] Release branch detected: ${BRANCH_NAME}"
          echo "[deploy] This is a placeholder. No heavy rendering and no live deploy performed on Jenkins."
          echo "[deploy] Here you could: rsync the published app to your render server, trigger a job, etc."
          # Example (commented):
          # rsync -avz --delete "${PUBLISH_DIR}/" user@render-host:/opt/poineer/render/
          # ssh user@render-host 'sudo systemctl restart poineer-render.service'
        '''
      }
    }

    stage('(Optional) Dry-Run Render Check') {
      when {
        allOf {
          branch 'develop'
          expression { return params?.RUN_RENDER_CHECK == true }
        }
      }
      steps {
        sh '''
          set -eux
          echo "[check] Running a short sanity-check invocation (no heavy work expected)."
          # Example of a quick, no-op style run if your app supports it:
          # dotnet "${PUBLISH_DIR}/POIneer.Render.dll" --help
        '''
      }
    }
  }

  parameters {
    // If you ever want to trigger a lightweight health check manually on develop
    booleanParam(name: 'RUN_RENDER_CHECK', defaultValue: false, description: 'Run a quick, no-op sanity check on develop (no heavy rendering).')
  }

  post {
    always {
      script {
        def emoji = currentBuild.currentResult == 'SUCCESS' ? '✅' : (currentBuild.currentResult == 'UNSTABLE' ? '⚠️' : '❌')
        echo "${emoji} Build result: ${currentBuild.currentResult}"
      }
    }
    failure {
      echo "Build failed. See logs above."
    }
  }
}
